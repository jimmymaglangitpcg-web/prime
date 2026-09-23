using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
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
        services.AddScoped<IRealPropertyUnitService, RealPropertyUnitService>();
        services.AddScoped<ITaxDeclarationService, TaxDeclarationService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<ISmvService, SmvService>();
        services.AddScoped<IAssessmentLevelService, AssessmentLevelService>();
        services.AddScoped<IValuationService, ValuationService>();

        return services;
    }
}
