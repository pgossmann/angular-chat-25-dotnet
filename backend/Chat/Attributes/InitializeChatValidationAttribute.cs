using System.ComponentModel.DataAnnotations;
using Chatbot.Chat.Models;

namespace Chatbot.Chat.Attributes;

public class InitializeChatValidationAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not InitializeChatRequest request)
        {
            return false;
        }

        // Either context must have content OR a file must be provided
        var hasContext = !string.IsNullOrWhiteSpace(request.Context);
        var hasFile = request.File != null;

        return hasContext || hasFile;
    }

    public override string FormatErrorMessage(string name)
    {
        return "Either context text or file must be provided for chat initialization.";
    }
}