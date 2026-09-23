using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using GmpPlatform.Core.Logging;

namespace GmpPlatform.Revit.ShopTickets;

/// <summary>
/// Stamps sheets this add-in created, so they can be found again by identity rather than by
/// guessing from their names.
/// <para>
/// Sheets used to be recognised by string-matching their name and number — <c>" TIF"</c>,
/// <c>"-2-"</c> and so on. That matches any GMP sheet that happens to be named similarly, and
/// on a real project it eventually will: the add-in would then reapply title blocks to
/// somebody else's drawing. Extensible storage is invisible to the drafter and cannot be
/// typed over by accident, which is what makes it the right place for an identity claim.
/// </para>
/// </summary>
internal static class ShopTicketMarker
{
    private static readonly Guid SchemaId = new("7C1B4B96-3D2E-4C6F-9B0A-2E5F4D8A1C37");
    private const string SchemaName = "GmpShopTicket";
    private const string MarkField = "Mark";

    private static Schema? GetOrCreateSchema()
    {
        var existing = Schema.Lookup(SchemaId);
        if (existing != null) return existing;

        try
        {
            var builder = new SchemaBuilder(SchemaId);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(MarkField, typeof(string));
            return builder.Finish();
        }
        catch (Exception ex)
        {
            PlatformLog.Logger.Warning(ex, "Could not create the shop-ticket schema");
            return null;
        }
    }

    /// <summary>Marks a sheet as ours. Must be called inside an open transaction.</summary>
    public static void Stamp(ViewSheet sheet, string mark)
    {
        try
        {
            var schema = GetOrCreateSchema();
            if (schema == null) return;

            var entity = new Entity(schema);
            entity.Set(MarkField, mark ?? "");
            sheet.SetEntity(entity);
        }
        catch (Exception ex)
        {
            // A sheet without a marker still works: the name heuristics remain as a fallback.
            PlatformLog.Logger.Warning(ex, "Could not stamp sheet {Number}", sheet.SheetNumber);
        }
    }

    /// <summary>The piece mark this sheet was generated for, or null if we did not create it.</summary>
    public static string? MarkOf(ViewSheet sheet)
    {
        try
        {
            var schema = Schema.Lookup(SchemaId);
            if (schema == null) return null;

            var entity = sheet.GetEntity(schema);
            return entity == null || !entity.IsValid() ? null : entity.Get<string>(MarkField);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsOurs(ViewSheet sheet) => MarkOf(sheet) != null;
}
