namespace ZdoRpgAi.Server.Catalog;

// One choice a service offers, a model or a voice. Name: what the service calls it; Size: of a
// local model, which tells what fits the GPU; Price: per million tokens, in / out.
public record CatalogItem(string Id, string? Name = null, string? Size = null, string? Price = null);

// A provider that lists its models, for picking in the settings panel: a cloud service those the
// key can use, a local server the ones installed
public interface IModelCatalog {
    Task<List<CatalogItem>> ListModelsAsync();
}

public interface IVoiceCatalog {
    Task<List<CatalogItem>> ListVoicesAsync();
}
