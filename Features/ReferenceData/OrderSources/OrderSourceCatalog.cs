namespace Luxira.Api.Features.ReferenceData.OrderSources;

public sealed record OrderSourceResponse(int Id, string Name, string LogoUrl);

public static class OrderSourceCatalog
{
    public static readonly OrderSourceResponse[] All =
    [
        new(1, "فيسبوك", "/socialmediaicons/facebook.svg"),
        new(2, "انستغرام", "/socialmediaicons/instagram.svg"),
        new(3, "سناب_شات", "/socialmediaicons/snapchat.svg"),
        new(4, "واتساب", "/socialmediaicons/whatsapp.svg"),
        new(5, "تك_توك", "/socialmediaicons/tiktok.svg"),
        new(6, "توتير", "/socialmediaicons/twitter.svg"),
        new(7, "ويبسات", "/socialmediaicons/internet.svg"),
        new(8, "اتصال", "/socialmediaicons/phone.svg"),
        new(9, "تلغرام", "/socialmediaicons/telegram.svg"),
        new(10, "ميتا", "/socialmediaicons/meta.svg"),
    ];
}
