namespace Habitica.Application.Inventory;

public sealed record InventoryActionResult(bool Succeeded, string Message)
{
    public static InventoryActionResult Success(string message) => new(true, message);

    public static InventoryActionResult Failure(string message) => new(false, message);
}
