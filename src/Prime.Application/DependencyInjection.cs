using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Buildings;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Features.Lands;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Billing.Rules;
using Prime.Application.Features.Gis;
using Prime.Application.Features.Gis.ReferenceLayers;
using Prime.Application.Features.Parcels;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.ReferenceData;
using Prime.Application.Features.Smv;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
using Prime.Application.Features.Valuation;

namespace Prime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPrimeApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<ITaxpayerService, TaxpayerService>();
        services.AddScoped<IParcelService, ParcelService>();
        services.AddScoped<IGisService, GisService>();
        services.AddScoped<IReferenceLayerService, ReferenceLayerService>();
        services.AddScoped<IBillingRuleService, BillingRuleService>();
        services.AddScoped<IRealPropertyUnitService, RealPropertyUnitService>();
        services.AddScoped<ITaxDeclarationService, TaxDeclarationService>();
        services.AddScoped<ILandService, LandService>();
        services.AddScoped<IBuildingService, BuildingService>();
        services.AddScoped<IMachineryService, MachineryService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<ISmvService, SmvService>();
        services.AddScoped<IAssessmentLevelService, AssessmentLevelService>();
        services.AddScoped<IValuationService, ValuationService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IGeneralRevisionService, GeneralRevisionService>();
        services.AddScoped<GeneralRevisionJobRunner>();

        return services;
    }
}
