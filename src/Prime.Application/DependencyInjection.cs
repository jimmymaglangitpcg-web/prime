using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Buildings;
using Prime.Application.Features.GeneralRevision;
using Prime.Application.Features.Lands;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Transactions;
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
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<Features.Collection.IPaymentService, Features.Collection.PaymentService>();
        services.AddScoped<Features.Collection.ICollectionSetupService, Features.Collection.CollectionSetupService>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<IApprovalChainService, ApprovalChainService>();
        services.AddScoped<IFormService, FormService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<INoticeService, NoticeService>();
        services.AddScoped<IFormDataProvider, NoticeFormDataProvider>();
        services.AddScoped<IAppraisalRecordService, AppraisalRecordService>();
        services.AddScoped<Features.Smv.IAdjustmentFactorService, Features.Smv.AdjustmentFactorService>();
        services.AddScoped<Features.Descriptions.IDescriptionService, Features.Descriptions.DescriptionService>();
        services.AddScoped<IFormDataProvider, AppraisalRecordFormDataProvider>();
        services.AddScoped<IFormDataProvider, StatementOfAccountFormDataProvider>();
        services.AddScoped<IFormDataProvider, FaasFormDataProvider>();
        services.AddScoped<IFormDataProvider, Features.Registers.RegisterFormDataProvider>();
        services.AddScoped<Features.Registers.IRegisterService, Features.Registers.RegisterService>();
        services.AddScoped<Features.SwornStatements.ISwornStatementService, Features.SwornStatements.SwornStatementService>();
        services.AddScoped<IFormDataProvider, Features.SwornStatements.SwornStatementFormDataProvider>();
        services.AddScoped<IFormDataProvider, TaxBillFormDataProvider>();
        services.AddScoped<IFormDataProvider, Features.Collection.PaymentFormDataProvider>();
        services.AddScoped<IFormDataProvider, TaxDeclarationFormDataProvider>();
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
