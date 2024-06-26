﻿function scrollToLastMessage()
{
    if (document.getElementById('MessagesInChatdiv')) {
        var elem = document.getElementById('MessagesInChatdiv');
        elem.scrollTop = elem.scrollHeight;
        return true;
    }
    return false;
}

class ProductsHelper {
    static dotNetHelper;

    static setDotNetHelper(value) {
        ProductsHelper.dotNetHelper = value;
    }

    static async giveReasoning(product, userPromptMessageId, chatCompletionMessageId) {
        await ProductsHelper.dotNetHelper.invokeMethodAsync('GiveProductReasoningAsync', product, userPromptMessageId, chatCompletionMessageId);
    }

    static async welcomeVisitor() {
        // const msg =
        //     await ProductsHelper.dotNetHelper.invokeMethodAsync('GetWelcomeMessage');
        //alert(`Message from .NET: "${msg}"`);
    }
}

window.ProductsHelper = ProductsHelper;