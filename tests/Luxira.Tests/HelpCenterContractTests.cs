using Luxira.Api.Features.Communication.Controllers;
using Luxira.Api.Features.Communication.Models;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class HelpCenterContractTests
{
    [Theory]
    [InlineData(nameof(HelpCenterChatController.List), "GET")]
    [InlineData(nameof(HelpCenterChatController.Search), "GET")]
    [InlineData(nameof(HelpCenterChatController.NewMessages), "GET")]
    [InlineData(nameof(HelpCenterChatController.SearchOrdersForLink), "GET")]
    [InlineData(nameof(HelpCenterChatController.MessageOrderLinks), "GET")]
    [InlineData(nameof(HelpCenterChatController.LinkMessageToOrder), "POST")]
    [InlineData(nameof(HelpCenterChatController.UnlinkMessageFromOrder), "POST")]
    [InlineData(nameof(HelpCenterChatController.OrderLinkedMessages), "GET")]
    [InlineData(nameof(HelpCenterChatController.AroundMessage), "GET")]
    [InlineData(nameof(HelpCenterChatController.UnreadCount), "GET")]
    [InlineData(nameof(HelpCenterChatController.Members), "GET")]
    [InlineData(nameof(HelpCenterChatController.Heartbeat), "POST")]
    [InlineData(nameof(HelpCenterChatController.ActivityStatus), "POST")]
    [InlineData(nameof(HelpCenterChatController.ResolveReference), "GET")]
    [InlineData(nameof(HelpCenterChatController.TriggerNegativeCommentsReminder), "POST")]
    [InlineData(nameof(HelpCenterChatController.GetKeywords), "GET")]
    [InlineData(nameof(HelpCenterChatController.SaveKeyword), "POST")]
    [InlineData(nameof(HelpCenterChatController.ToggleKeywordActive), "POST")]
    [InlineData(nameof(HelpCenterChatController.DeleteKeyword), "POST")]
    [InlineData(nameof(HelpCenterChatController.DeleteKeywords), "POST")]
    [InlineData(nameof(HelpCenterChatController.GetKeywordCategories), "GET")]
    [InlineData(nameof(HelpCenterChatController.Media), "GET")]
    [InlineData(nameof(HelpCenterChatController.Pinned), "GET")]
    [InlineData(nameof(HelpCenterChatController.Send), "POST")]
    [InlineData(nameof(HelpCenterChatController.Edit), "POST")]
    [InlineData(nameof(HelpCenterChatController.EditHistory), "GET")]
    [InlineData(nameof(HelpCenterChatController.ToggleReaction), "POST")]
    [InlineData(nameof(HelpCenterChatController.TogglePin), "POST")]
    [InlineData(nameof(HelpCenterChatController.Delete), "POST")]
    [InlineData(nameof(HelpCenterChatController.DeleteMany), "POST")]
    [InlineData(nameof(HelpCenterChatController.DeleteForMe), "POST")]
    [InlineData(nameof(HelpCenterChatController.HardDelete), "POST")]
    [InlineData(nameof(HelpCenterChatController.MarkRead), "POST")]
    [InlineData(nameof(HelpCenterChatController.Readers), "GET")]
    [InlineData(nameof(HelpCenterChatController.Settings), "GET")]
    [InlineData(nameof(HelpCenterChatController.UpdateSettings), "POST")]
    [InlineData(nameof(HelpCenterChatController.Attachment), "GET")]
    public void LegacyActionRouteExists(string actionName, string httpMethod)
    {
        var methods = typeof(HelpCenterChatController).GetMethods()
            .Where(method => method.Name == actionName)
            .ToList();

        Assert.Contains(methods, method => method
            .GetCustomAttributes(inherit: true)
            .OfType<HttpMethodAttribute>()
            .Any(attribute =>
                attribute.HttpMethods.Contains(httpMethod) &&
                string.Equals(attribute.Template, actionName, StringComparison.Ordinal)));
    }

    [Fact]
    public void MessageKeysMatchLegacyBigIntSchema()
    {
        Assert.Equal(typeof(long), typeof(HelpCenterChatMessage).GetProperty(nameof(HelpCenterChatMessage.Id))!.PropertyType);
        Assert.Equal(typeof(long?), typeof(HelpCenterChatMessage).GetProperty(nameof(HelpCenterChatMessage.ReplyToMessageId))!.PropertyType);
        Assert.Equal(typeof(long), typeof(HelpCenterChatUserPresence).GetProperty(nameof(HelpCenterChatUserPresence.Id))!.PropertyType);
    }
}
