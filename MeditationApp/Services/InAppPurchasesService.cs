// filepath: /Users/scotttopping/RiderProjects/MeditationApp/MeditationApp/Services/InAppPurchaseService.cs

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Plugin.InAppBilling;

namespace MeditationApp.Services;

public class InAppPurchaseService
{
    public async Task<bool> PurchaseSubscriptionAsync(string productId)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
                return false;

            var purchase = await billing.PurchaseAsync(productId, ItemType.Subscription, "apppayload");
            return purchase != null && purchase.State == PurchaseState.Purchased;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync()
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
                return Enumerable.Empty<InAppBillingPurchase>();

            return await billing.GetPurchasesAsync(ItemType.Subscription) ?? Enumerable.Empty<InAppBillingPurchase>();
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }
}