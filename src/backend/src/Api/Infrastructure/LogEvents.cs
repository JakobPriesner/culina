namespace Api.Infrastructure;

/// <summary>
/// The event-id blocks, allocated per domain.
/// </summary>
/// <remarks>
/// Ids are allocated in blocks so an alert can key on an id that survives a
/// message being reworded. A <c>LoggerMessage</c> declares its id inline; this
/// file is the register that stops two domains claiming the same number.
///
/// <list type="table">
///   <item><term>1000-1099</term><description>Users and authentication</description></item>
///   <item><term>1100-1199</term><description>Households and invitations</description></item>
///   <item><term>1200-1299</term><description>Recipes</description></item>
///   <item><term>1300-1399</term><description>Cooking</description></item>
///   <item><term>1400-1499</term><description>Shopping</description></item>
///   <item><term>1500-1599</term><description>The assistant</description></item>
///   <item><term>1600-1699</term><description>Setup, and restarts to apply server settings</description></item>
///   <item><term>1800-1899</term><description>Request pipeline and security</description></item>
///   <item><term>1900-1999</term><description>Infrastructure: migrations, storage</description></item>
/// </list>
/// </remarks>
internal static class LogEvents
{
    internal const int UsersBase = 1000;
    internal const int HouseholdsBase = 1100;
    internal const int RecipesBase = 1200;
    internal const int CookingBase = 1300;
    internal const int ShoppingBase = 1400;
    internal const int AssistanceBase = 1500;
    internal const int ServerBase = 1600;
    internal const int PipelineBase = 1800;
    internal const int InfrastructureBase = 1900;
}
