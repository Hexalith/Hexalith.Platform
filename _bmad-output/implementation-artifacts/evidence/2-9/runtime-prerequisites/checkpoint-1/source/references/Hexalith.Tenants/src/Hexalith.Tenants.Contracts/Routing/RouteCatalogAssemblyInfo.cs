using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Tenants.Contracts.Routing;

#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
[assembly: RouteCatalogDeclarationSource(typeof(TenantCatalogDeclarations))]
#endif
