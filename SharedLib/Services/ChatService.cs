using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharedLib.Constants;
using SharedLib.Exceptions;
using SharedLib.Extensions;
using SharedLib.Models;
using SharpToken;

namespace SharedLib.Services;

public class ChatService
{
    /// <summary>
    /// All data is cached in the _sessions List object.
    /// </summary>
    private List<Session> _sessions = new();

    private readonly OpenAiService _openAiService;
    private readonly MongoDbService _mongoDbService;
    private readonly int _maxConversationTokens;
    private readonly int _maxCompletionTokens;
    private readonly ILogger _logger;

    public const string DefaultCollection = "clothes";
    public const string DefaultProductCategory = "Others";

    private const string UserPromptMarker = "[USER_PROMPT]";
    private const string ChatCompletionMarker = "[CHAT_COMPLETION]";
    private const string ProductMarker = "[PRODUCT]";
    private const string CategoriesListMarker = "[CATEGORIES_LIST]";

    public const string UserPromptTemplate = @"Below is my story. I am trying to purchase some apparel.
---
### STORY ###
[USER_PROMPT]
---
Tell me who I might be and what my needs are. Suggests characteristics of the apparel I might be interested in.";

    public const string UserAttributesPromptTemplate = @"Below is my story. I am trying to purchase some apparel.
---
### STORY ###
[USER_PROMPT]
---
Return JSON object based on the user story with the following keys. 
- gender; values must be from the following list: Boys, Girls, Womens, Mens, Unisex, Undefined. If you cannot determine the gender, use Undefined.
- minPrice; numeric value of the minimum price desired by the user. If you cannot determine the minimum desired price, use 0.
- maxPrice; numeric value of the maximum price desired by the user. If you cannot determine the maximum desired price, use 0.
 
Be precise. Do not show reasoning. You MUST return JSON object.
### INPUT ###
I am going to play soccer with my friends
### OUTPUT ###
{""gender"":""Undefined""}
### INPUT ###
I am going to play soccer with my friends. I am a man.
### OUTPUT ###
{""gender"":""Mens""}
### INPUT ###
I am looking for clothes for my daughter's tennis game on the weekend. I prefer to spend not more than 200 bucks.
### OUTPUT ###
{""gender"":""Girls"", ""minPrice"": 0, ""maxPrice"": 200}
### INPUT ###
I am looking for something special for my date night.
### OUTPUT ###
{""gender"":""Womens""}
### INPUT ###
I am looking for something special for my date night. I want something special from $400
### OUTPUT ###
{""gender"":""Womens"", ""minPrice"": 400, ""maxPrice"":0}";

    private const string ProductReasoningTemplate = @"Below is my story. I am trying to purchase apparel:
---
STORY
[USER_PROMPT]
---
Bellow is what product I picked:
---
PRODUCT
[PRODUCT]
---
Now tell me why you think I may like the product. Explain your reasoning.
";

    private const string WhyLikeProductTemplate = @"
Why you may like it?

[PRODUCT]

[CHAT_COMPLETION]
";

    public const string ExtraQuestionsPromptTemplate = @"Below is my story. I am trying to purchase some apparel.
---
### STORY ###
[USER_PROMPT]
---
Think of 5 additional apparel pieces I might be interested in based on my story. You MUST return a JSON array of 5 strings.
Be precise. Do not show reasoning.
### INPUT ###
I am looking for something special for my date night.
### OUTPUT ###
[""Black dress"", ""High heels"", ""Elegant jewelry"", ""Red lipstick"", ""Stylish handbag""]
### INPUT ###
I have a business meeting planned
### OUTPUT ###
[""Formal suit"", ""Tie"", ""Leather shoes"", ""Briefcase"", ""Wristwatch""]";

    public const string ProductCategoriesPromptTemplate = @"You are given list of categories in machine format.
------
### CATEGORIES ###
[CATEGORIES_LIST]
-------
- Group categories into exactly 5 groups based on similarity and popularity.
- You MUST name each group summarizing categories in this group, use not more than 5 words for name.
- Map each of given categories to one of the named groups.
Be precise. Do not show reasoning. You MUST return JSON dictionary with EACH product categories as keys and mapped groups as values.
-------
Few examples:
### INPUT ###
girls~clothing~dresses jumpsuits~day|girls~clothing~dresses jumpsuits~party special occasion
mens~clothing~shirts~classic
boys~clothing~pajamas~slippers|boys~shoes~slippers
womens~clothing~dresses jumpsuits|womens~features~new arrivals
womens~shoes~boots
womens~shoes~heels
null
boys~clothing~pajamas~slippers|boys~shoes~slippers
### OUTPUT ###
{
    ""girls~clothing~dresses jumpsuits~day|girls~clothing~dresses jumpsuits~party special occasion"": ""Girl's Clothing"",
    ""mens~clothing~shirts~classic"": ""Men's Clothing"",
    ""boys~clothing~pajamas~slippers|boys~shoes~slippers"": ""Boy's Clothing"",
    ""womens~clothing~dresses jumpsuits|womens~features~new arrivals"": ""Women's Clothing"",
    ""womens~shoes~boots"": ""Women's Shoes"",
    ""womens~shoes~heels"": ""Women's Shoes""
}
### INPUT ###
girls~clothing~dresses jumpsuits~day|girls~clothing~dresses jumpsuits~party special occasion
mens~clothing~shirts~classic
mens~clothing~shirts~classic
mens~clothing~shirts~classic
mens~clothing~t-shirts
mens~clothing~t-shirts
mens~clothing~t-shirts
boys~clothing~pajamas~slippers|boys~shoes~slippers
### OUTPUT ###
{
    ""girls~clothing~dresses jumpsuits~day|girls~clothing~dresses jumpsuits~party special occasion"": ""Girl's Clothing"",
    ""mens~clothing~shirts~classic"": ""Men's Clothing"",
    ""mens~clothing~t-shirts"": ""Men's Clothing"",
    ""boys~clothing~pajamas~slippers|boys~shoes~slippers"": ""Boy's Clothing""
}";

    public ChatService(OpenAiService openAiService, MongoDbService mongoDbService, ILogger logger)
    {

        _openAiService = openAiService;
        _mongoDbService = mongoDbService;

        _maxConversationTokens = openAiService.MaxConversationTokens;
        _maxCompletionTokens = openAiService.MaxCompletionTokens;
        _logger = logger;
    }

    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _mongoDbService.GetSessionsAsync();
    }


    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        await CheckIfSessionCached(sessionId);

        List<Message> chatMessages = new();

        if (_sessions.Count == 0)
        {
            return Enumerable.Empty<Message>().ToList();
        }

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        if (_sessions[index].Messages.Count == 0)
        {
            // Messages are not cached, go read from database
            chatMessages = await _mongoDbService.GetSessionMessagesAsync(sessionId);

            // Cache results
            _sessions[index].Messages = chatMessages;
        }
        else
        {
            // Load from cache
            chatMessages = _sessions[index].Messages;
        }

        return chatMessages;
    }

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    public async Task<string> CreateNewChatSessionAsync()
    {
        Session session = new();

        _sessions.Add(session);

        await _mongoDbService.InsertSessionAsync(session);

        return session.SessionId;
    }

    /// <summary>
    /// Rename the Chat Session from "New Chat" to the summary provided by OpenAI
    /// </summary>
    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].Name = newChatSessionName;

        await _mongoDbService.UpdateSessionAsync(_sessions[index]);
    }

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    public async Task DeleteChatSessionAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions.RemoveAt(index);

        await _mongoDbService.DeleteSessionAndMessagesAsync(sessionId);
    }

    /// <summary>
    /// Receive a prompt from a user, Vectorize it from _openAIService Get a completion from _openAiService
    /// </summary>
    public async Task<string> GetChatCompletionRAGAsync(string? sessionId, string userPrompt, string collectionName)
    {

        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);


            //Get embeddings for user prompt and number of tokens it uses.
            (float[] promptVectors, int promptTokens) = await _openAiService.GetEmbeddingsAsync(sessionId, userPrompt);
            //Create the prompt message object. Created here to give it a timestamp that precedes the completion message.
            Message promptMessage = new Message(sessionId, nameof(Participants.User), promptTokens, default, userPrompt);


            //Do vector search on the user prompt, return list of documents
            string retrievedDocuments = await _mongoDbService.VectorSearchAsync(collectionName, promptVectors);

            //Get the most recent conversation history up to _maxConversationTokens
            string conversation = GetConversationHistory(sessionId);


            //Construct our prompts sent to Azure OpenAI. Calculate token usage and trim the RAG payload and conversation history to prevent exceeding token limits.
            (string augmentedContent, string conversationAndUserPrompt) = BuildPrompts(userPrompt, conversation, retrievedDocuments);


            //Generate the completion from Azure OpenAI to return to the user
            (string completionText, int ragTokens, int completionTokens) = await _openAiService.GetChatCompletionAsync(sessionId, conversationAndUserPrompt, augmentedContent);


            //Create the completion message object
            Message completionMessage = new Message(sessionId, nameof(Participants.Assistant), completionTokens, ragTokens, completionText);

            //Add the user prompt and completion to cache, then persist to Cosmos in a transaction
            await AddPromptCompletionMessagesAsync(sessionId, promptMessage, completionMessage);


            return completionText;

        }
        catch (Exception ex)
        {
            string message = $"ChatService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }




    public async Task<List<Message>> GetChatCompletionProductSearchAsync(string? sessionId, string userPrompt, string collectionName, string userPromptTemplate, string customerAttributesPromptTemplate, string extraQuestionsPromptTemplate)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            await CheckIfSessionCached(sessionId);

            // Get the most recent conversation history up to _maxConversationTokens
            string userMessages = GetConversationHistory(sessionId, sender: nameof(Participants.User));

            // 1. Get extended desired products search text by chat
            var (productSearchText, promptTokensProductSearchText, completionTokensProductSearchText) = await GetProductSearchText(sessionId, userPromptTemplate, userMessages, userPrompt);


            // 2. Get customer attributes by chat
            var (userAttributes, promptTokensUserAttributes, completionTokensUserAttributes) = await GetCustomerAttributes(sessionId, customerAttributesPromptTemplate, userMessages, userPrompt);

            // 3. Get extra questions for chat
            var (extraQuestions, promptTokensExtraQuestion, completionTokensExtraQuestions) = await GetExtraQuestions(sessionId, extraQuestionsPromptTemplate, userMessages, userPrompt);

            // 4. Find products
            var (products, tokensProductSearchEmbeddings) = await GetProducts(sessionId, productSearchText, userMessages, userPrompt, collectionName, userAttributes);

            // 5. Get product categories by chat
            var (promptTokensProductCategories, completionTokensProductCategories) = await PopulateProductCategoriesAsync(sessionId, products);

            int promptTokensTotal = promptTokensProductSearchText + promptTokensUserAttributes + promptTokensExtraQuestion + promptTokensProductCategories;
            int completionTokensTotal = completionTokensProductSearchText + completionTokensUserAttributes + completionTokensExtraQuestions + completionTokensProductCategories + tokensProductSearchEmbeddings;

            // Create the prompt message object. Created here to give it a timestamp that precedes the completion message.
            Message userMessage = new Message(sessionId, sender: nameof(Participants.User), tokens: default, promptTokens: default, text: userPrompt);

            // Create the completion message object to have it's id
            Message assistantMessage = new Message(
                sessionId,
                sender: nameof(Participants.Assistant),
                tokens: completionTokensTotal,
                promptTokens: promptTokensTotal,
                text: productSearchText,
                products,
                userAttributes,
                extraQuestions);

            //Add the user prompt and completion to cache, then persist to Cosmos in a transaction
            await AddPromptCompletionMessagesAsync(sessionId, userMessage, assistantMessage);

            return new List<Message>
            {
                userMessage,
                assistantMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("{ChatCompletionProductSearchAsyncName}: {ExMessage}", nameof(GetChatCompletionProductSearchAsync), ex.Message);
            throw;
        }
    }

    private async Task<(int promptTokensProductCategories, int completionTokensProductCategories)> PopulateProductCategoriesAsync(string sessionId, List<ClothesProduct> products)
    {
        string productCategoriesList = string.Join("\n", products
            .Select(p => p.Categories)
            .Where(category => !string.IsNullOrWhiteSpace(category))
        );

        // Get human-readable product categories by chat
        var (productCategories, promptTokensProductCategories, completionTokensProductCategories) = await GetProductCategories(sessionId, ProductCategoriesPromptTemplate, productCategoriesList);
        if(productCategories is null || productCategories.Count == 0)
        {
            // set default category
            foreach (var product in products)
            {
                product.Category = DefaultProductCategory;
            }
            return (promptTokensProductCategories, completionTokensProductCategories);
        }

        // Add product categories to the products
        foreach (var product in products)
        {
            if (product.Categories is null)
            {
                product.Category = DefaultProductCategory;
                continue;
            }

            if (productCategories.TryGetValue(product.Categories, out var category))
            {
                product.Category = category;
            }
            else
            {
                // Try split categories by | and find any of them in the productCategories
                var categories = product.Categories.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var cat in categories)
                {
                    if (productCategories.TryGetValue(cat, out category))
                    {
                        product.Category = category;
                        break;
                    }
                }
                // if no category found, set it to "Others"
                product.Category ??= DefaultProductCategory;
            }
        }

        return (promptTokensProductCategories, completionTokensProductCategories);
    }

    private async Task<(CustomerAttributes?, int, int)> GetCustomerAttributes(string sessionId, string userAttributesPromptTemplate, string userMessages, string userPrompt)
    {
        string userAttributesJson = "";
        int promptTokensUserAttributes = 0;
        int completionTokensUserAttributes = 0;

        try
        {
            string userAttributesPrompt = string.IsNullOrWhiteSpace(userAttributesPromptTemplate) ? UserAttributesPromptTemplate : userAttributesPromptTemplate;
            userAttributesPrompt = userAttributesPrompt.Replace(UserPromptMarker, userMessages + "\n" + userPrompt);

            (string augmentedContentUserAttributes, string conversationAndUserPromptUserAttributes) = BuildPrompts(userAttributesPrompt, conversation: "", retrievedData: "");

            // Generate the completion from Azure OpenAI to have product extended description by chat
            (userAttributesJson, promptTokensUserAttributes, completionTokensUserAttributes) = await _openAiService.GetChatCompletionAsync(sessionId, conversationAndUserPromptUserAttributes, documents: augmentedContentUserAttributes);


            var customerAttributes = JsonConvert.DeserializeObject<CustomerAttributes>(userAttributesJson);
            customerAttributes?.Sanitize();
            return (customerAttributes, promptTokensUserAttributes, completionTokensUserAttributes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot get {UserAttributes} from: {UserAttributeJson}, ex: {Exception}", nameof(CustomerAttributes), userAttributesJson, ex.Message);
            return (null, promptTokensUserAttributes, completionTokensUserAttributes);
        }
    }

    private async Task<(List<string>?, int, int)> GetExtraQuestions(string sessionId, string extraQuestionsPromptTemplate, string userMessages, string userPrompt)
    {
        string extraQuestionsJson = "";
        int promptTokensExtraQuestions = 0;
        int completionTokensExtraQuestions = 0;

        try
        {
            string extraQuestionsPrompt = string.IsNullOrWhiteSpace(extraQuestionsPromptTemplate) ? ExtraQuestionsPromptTemplate : extraQuestionsPromptTemplate;
            extraQuestionsPrompt = extraQuestionsPrompt.Replace(UserPromptMarker, userMessages + "\n" + userPrompt);

            (string augmentedContentExtraQuestions, string conversationAndUserPromptExtraQuestions) = BuildPrompts(extraQuestionsPrompt, conversation: "", retrievedData: "");

            // Generate extra questions
            (extraQuestionsJson, promptTokensExtraQuestions, completionTokensExtraQuestions) = await _openAiService.GetChatCompletionAsync(sessionId, conversationAndUserPromptExtraQuestions, documents: augmentedContentExtraQuestions);


            var extraQuestions = JsonConvert.DeserializeObject<List<string>?>(extraQuestionsJson);
            return (extraQuestions, promptTokensExtraQuestions, completionTokensExtraQuestions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot get Extra Questions list from: {ExtraQuestionsJson}, ex: {Exception}",extraQuestionsJson, ex.Message);
            return (null, promptTokensExtraQuestions, completionTokensExtraQuestions);
        }
    }

    private async Task<(Dictionary<string, string>?, int, int)> GetProductCategories(string sessionId, string productCategoriesPromptTemplate, string productCategoriesList)
    {
        string productCategoriesJson = "";
        int promptTokensProductCategories = 0;
        int completionTokensProductCategories = 0;

        try
        {
            string productCategoriesPrompt = string.IsNullOrWhiteSpace(productCategoriesPromptTemplate) ? ProductCategoriesPromptTemplate : productCategoriesPromptTemplate;
            productCategoriesPrompt = productCategoriesPrompt.Replace(CategoriesListMarker, productCategoriesList);

            (string augmentedContentProductCategories, string conversationAndUserPromptProductCategories) = BuildPrompts(productCategoriesPrompt, conversation: "", retrievedData: "");

            (productCategoriesJson, promptTokensProductCategories, completionTokensProductCategories) = await _openAiService.GetChatCompletionAsync(
                sessionId,
                conversationAndUserPromptProductCategories,
                documents: augmentedContentProductCategories,
                systemMessage: "You are an AI assistant that helps people find information.");

            var productCategories = JsonConvert.DeserializeObject<Dictionary<string, string>?>(productCategoriesJson);
            return (productCategories, promptTokensProductCategories, completionTokensProductCategories);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot get Product Categories from: {ProductCategoriesJson}, ex: {Exception}",productCategoriesJson, ex.Message);
            return (null, promptTokensProductCategories, completionTokensProductCategories);
        }
    }

    private async Task<(string, int, int)> GetProductSearchText(string sessionId, string userPromptTemplate, string userMessages, string userPrompt)
    {
        string productSearchPrompt = string.IsNullOrWhiteSpace(userPromptTemplate) ? UserPromptTemplate : userPromptTemplate;
        // Extend user prompt with predefined template with PARAGRAPH
        productSearchPrompt = productSearchPrompt.Replace(UserPromptMarker, userMessages + "\n" + userPrompt);

        // Construct our prompts: here we can use previous conversation ans maybe some data context.
        // We omit conversation and data for now.
        // Trim payload to prevent exceeding token limits.
        (string augmentedContent, string trimmedProductSearchPrompt) = BuildPrompts(productSearchPrompt, conversation: "", retrievedData: "");

        // Generate the completion from Azure OpenAI to have desired products description by chat
        (string completionText, int promptTokens, int completionTokens) = await _openAiService.GetChatCompletionAsync(sessionId, trimmedProductSearchPrompt, documents: augmentedContent);

        return (completionText, promptTokens, completionTokens);
    }

    private async Task CheckIfSessionCached(string sessionId)
    {
        if (!_sessions.Exists(session => session.SessionId == sessionId)) // session is not cached, go read from database
        {
            Session? session = await _mongoDbService.GetSessionAsync(sessionId);
            if(session != null)
            {
                List<Message> messages = await _mongoDbService.GetSessionMessagesAsync(sessionId);
                session.Messages = messages;
                _sessions.Add(session);
            }
            else
            {
                _logger.LogWarning("Session with id {SessionId} not found in the database", sessionId);
                throw new ApiException($"Session {sessionId} not found", HttpStatusCode.NotFound);
            }
        }
    }

    public async Task GetProductReasoningAsync(string? sessionId, string productId)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            ClothesProduct? product = await _mongoDbService.GetClothesProductAsync(productId);
            ArgumentNullException.ThrowIfNull(product);

            // Get the most recent conversation history up to _maxConversationTokens
            string userMessages = GetConversationHistory(sessionId, nameof(Participants.User));
            string productCard = $"<img src=\"{product.ImageUrl}\" alt=\"Description of image\" class=\"thumbnail15\"><br/>" + product.ToFormattedString();

            string prompt = ProductReasoningTemplate
                .Replace(UserPromptMarker, userMessages)
                .Replace(ProductMarker, productCard);

            (string augmentedContent, string conversationAndUserPrompt) =
                BuildPrompts(prompt, conversation: "", retrievedData: "");


            // Generate the completion from Azure OpenAI to have product tags
            (string completionText, int ragTokens, int completionTokens) =
                await _openAiService.GetChatCompletionAsync(
                    sessionId,
                    conversationAndUserPrompt,
                    documents: augmentedContent
                    );

            string message = WhyLikeProductTemplate
                .Replace(ProductMarker, productCard)
                .Replace(ChatCompletionMarker, completionText);

            // Create the completion message object to have it's id
            Message chatCompletionMessage = new Message(sessionId, nameof(Participants.Assistant), tokens: completionTokens,
                promptTokens: ragTokens, text: message);

            //Add the user prompt and completion to cache, then persist to Cosmos in a transaction
            await AddPromptCompletionMessagesAsync(sessionId, chatCompletionMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError("{GetProductReasoningAsyncName}: {ExMessage}", nameof(GetProductReasoningAsync), ex.Message);
            throw;
        }
    }

    private async Task<(List<ClothesProduct>, int)> GetProducts(string sessionId, string productSearchText, string userMessages, string userPrompt, string collectionName, CustomerAttributes? userAttributes)
    {
        string productEmbeddingsPrompt = userMessages + "\n" + userPrompt + "\n" + productSearchText;
        // Get embeddings for chat completion
        (float[] productSearchEmbeddings, int tokensProductSearchEmbeddings) = await _openAiService.GetEmbeddingsAsync(sessionId, productEmbeddingsPrompt);

        // Do vector search on tags found by user prompt
        string retrievedDocuments = await _mongoDbService.VectorSearchAsync(collectionName, productSearchEmbeddings);

        // Deserialize BsonDocuments to a list of C# objects (ClothesProduct model)
        List<ClothesProduct> clothesProducts = ClothesProductExtensions.GetProducts(retrievedDocuments);
        List<ClothesProduct> filteredProducts = clothesProducts.ToList();

        // Filter found products by user attributes
        if (userAttributes != null)
        {
            filteredProducts = clothesProducts
                .Where(cp => userAttributes.Gender == null || cp.Gender == userAttributes.Gender)
                .Where(cp => userAttributes.MinPrice == null || userAttributes.MinPrice == 0 || Equals(userAttributes.MinPrice, userAttributes.MaxPrice) || cp.Price >= userAttributes.MinPrice)
                .Where(cp => userAttributes.MaxPrice == null || userAttributes.MaxPrice == 0 || Equals(userAttributes.MaxPrice, userAttributes.MinPrice) || cp.Price <= userAttributes.MaxPrice)
                .ToList();

            // If no products matching, return all products
            if (filteredProducts.Count == 0)
            {
                filteredProducts = clothesProducts.ToList();
            }
        }

        return (filteredProducts, tokensProductSearchEmbeddings);
    }



    /// <summary>
    /// Estimate the token usage for OpenAI completion to prevent exceeding the OpenAI model's maximum token limit. This function estimates the
    /// amount of tokens the vector search result data and the user prompt will consume. If the search result data exceeds the configured amount
    /// the function reduces the number of vectors, reducing the amount of data sent.
    /// </summary>
    private (string augmentedContent, string conversationAndUserPrompt) BuildPrompts(string userPrompt, string conversation, string retrievedData)
    {

        string updatedAugmentedContent = "";
        string updatedConversationAndUserPrompt = "";


        //SharpToken only estimates token usage and often undercounts. Add a buffer of 200 tokens.
        int bufferTokens = 200;

        //Create a new instance of SharpToken
        var encoding = GptEncoding.GetEncoding("cl100k_base");  //encoding base for GPT 3.5 Turbo and GPT 4
        //var encoding = GptEncoding.GetEncodingForModel("gpt-35-turbo");

        List<int> ragVectors = encoding.Encode(retrievedData);
        int ragTokens = ragVectors.Count;

        List<int> convVectors = encoding.Encode(conversation);
        int convTokens = convVectors.Count;

        int userPromptTokens = encoding.Encode(userPrompt).Count;


        //If RAG data plus user prompt, plus conversation, plus tokens for completion is greater than max completion tokens we've defined, reduce the rag data and conversation by relative amount.
        int totalTokens = ragTokens + convTokens + userPromptTokens + bufferTokens;

        //Too much data, reduce the rag data and conversation data by the same percentage. Do not reduce the user prompt as this is required for the completion.
        if (totalTokens > _maxCompletionTokens)
        {
            //Get the number of tokens to reduce by
            int tokensToReduce = totalTokens - _maxCompletionTokens;

            //Get the percentage of tokens to reduce by
            float ragTokenPct = (float)ragTokens / totalTokens;
            float conTokenPct = (float)convTokens / totalTokens;

            //Calculate the new number of tokens for each data set
            int newRagTokens = (int)Math.Round(ragTokens - (ragTokenPct * tokensToReduce), 0);
            int newConvTokens = (int)Math.Round(convTokens - (conTokenPct * tokensToReduce), 0);


            //Get the reduced set of RAG vectors
            List<int> trimmedRagVectors = ragVectors.GetRange(0, newRagTokens);
            //Convert the vectors back to text
            updatedAugmentedContent = encoding.Decode(trimmedRagVectors);

            int offset = convVectors.Count - newConvTokens;

            //Get the reduce set of conversation vectors
            List<int> trimmedConvVectors = convVectors.GetRange(offset, newConvTokens);

            //Convert vectors back into reduced conversation length
            updatedConversationAndUserPrompt = encoding.Decode(trimmedConvVectors);

            //add user prompt
            updatedConversationAndUserPrompt += Environment.NewLine + userPrompt;


        }
        //If everything is less than _maxCompletionTokens then good to go.
        else
        {

            //Return all of the content
            updatedAugmentedContent = retrievedData;
            updatedConversationAndUserPrompt = conversation + Environment.NewLine + userPrompt;
        }


        return (augmentedContent: updatedAugmentedContent, conversationAndUserPrompt: updatedConversationAndUserPrompt);

    }

    /// <summary>
    /// Get the most recent conversation history to provide additional context for the completion LLM
    /// </summary>
    private string GetConversationHistory(string sessionId, string? sender = null)
    {
        int? tokensUsed = 0;

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        List<Message> conversationMessages = _sessions[index].Messages.ToList(); //make a full copy

        // Iterate through these in reverse order to get the most recent conversation history up to _maxConversationTokens
        var trimmedMessages = conversationMessages
            .Where(m=> string.IsNullOrWhiteSpace(sender) || m.Sender == sender)
            .OrderByDescending(m => m.TimeStamp)
            .TakeWhile(m => (tokensUsed += m.Tokens) <= _maxConversationTokens)
            .Select(m => m.Text)
            .ToList();

        trimmedMessages.Reverse();

        // Return as a string
        string conversation = string.Join(Environment.NewLine, trimmedMessages.ToArray());

        return conversation;

    }

    public async Task<string> SummarizeChatSessionNameAsync(string? sessionId, string prompt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        string response = await _openAiService.SummarizeAsync(sessionId, prompt);
        if (response.Length > 30)
            response = response.Substring(0, 30) + "...";

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

    /// <summary>
    /// Add user prompt to the chat session message list object and insert into the data service.
    /// </summary>
    private async Task AddPromptMessageAsync(string sessionId, string promptText)
    {
        Message promptMessage = new(sessionId, nameof(Participants.User), default, default, promptText);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].AddMessage(promptMessage);

        await _mongoDbService.InsertMessageAsync(promptMessage);
    }


    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string sessionId, Message userPromptMessage, Message chatCompletionMessage)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);


        //Add prompt and completion to the cache
        _sessions[index].AddMessage(userPromptMessage);
        _sessions[index].AddMessage(chatCompletionMessage);


        //Update session cache with tokens used
        _sessions[index].TokensUsed += userPromptMessage.Tokens;
        _sessions[index].TokensUsed += chatCompletionMessage.PromptTokens;
        _sessions[index].TokensUsed += chatCompletionMessage.Tokens;

        await _mongoDbService.UpsertSessionBatchAsync(session: _sessions[index], userPromptMessage, chatCompletionMessage);

    }

    private async Task AddPromptCompletionMessagesAsync(string sessionId, Message chatCompletionMessage)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);


        //Add prompt and completion to the cache
        _sessions[index].AddMessage(chatCompletionMessage);


        //Update session cache with tokens used
        _sessions[index].TokensUsed += chatCompletionMessage.PromptTokens;
        _sessions[index].TokensUsed += chatCompletionMessage.Tokens;

        await _mongoDbService.UpsertSessionBatchAsync(session: _sessions[index], chatCompletionMessage);

    }
}