﻿namespace MefContrib.Hosting.Interception.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition.Hosting;
    using System.ComponentModel.Composition.Primitives;
    using System.Linq;

    public class GenericExportHandler : IExportHandler
    {
        private ComposablePartCatalog decoratedCatalog;
        private readonly AggregateCatalog aggregateCatalog;
        private readonly IDictionary<Type, Type> genericTypeMapping;
        private readonly List<Type> manufacturedParts;

        public GenericExportHandler()
        {
            this.aggregateCatalog = new AggregateCatalog();
            this.genericTypeMapping = new Dictionary<Type, Type>();
            this.manufacturedParts = new List<Type>();
        }

        #region IExportHandler Members

        public void Initialize(ComposablePartCatalog interceptedCatalog)
        {
            this.decoratedCatalog = interceptedCatalog;
            LoadTypeMappings();
        }

        private void LoadTypeMappings()
        {
            using (var ep = new CatalogExportProvider(this.decoratedCatalog))
            {
                ep.SourceProvider = ep;
                var locators = ep.GetExportedValues<IGenericContractRegistry>();
                
                foreach (var mapping in locators.SelectMany(locator => locator.GetMappings()))
                {
                    this.genericTypeMapping.Add(
                        mapping.GenericContractTypeDefinition,
                        mapping.GenericImplementationTypeDefinition);
                }
            }
        }

        private void CreateGenericPart(Type importDefinitionType)
        {
            var type = TypeHelper.BuildGenericType(importDefinitionType, this.genericTypeMapping);

            this.manufacturedParts.Add(importDefinitionType);
            this.aggregateCatalog.Catalogs.Add(new TypeCatalog(type));
        }

        public IEnumerable<Tuple<ComposablePartDefinition, ExportDefinition>> GetExports(ImportDefinition definition, IEnumerable<Tuple<ComposablePartDefinition, ExportDefinition>> exports)
        {
            if (exports.Any())
            {
                return exports;
            }

            var contractDef = (ContractBasedImportDefinition)definition;
            var returnedExports = new List<Tuple<ComposablePartDefinition, ExportDefinition>>();
            var importDefinitionType = TypeHelper.GetImportDefinitionType(definition);
            
            if (this.manufacturedParts.Contains(importDefinitionType))
            {
                returnedExports.AddRange(this.aggregateCatalog.GetExports(definition));
            }
            else if (TypeHelper.ShouldCreateClosedGenericPart(contractDef, importDefinitionType))
            {
                CreateGenericPart(importDefinitionType);
                returnedExports.AddRange(this.aggregateCatalog.GetExports(definition));
            }
            
            return returnedExports;
        }

        #endregion
    }
}
