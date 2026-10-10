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
        services.AddScoped<ILandValueMapService, LandValueMapService>();
        services.AddScoped<IReferenceLayerService, ReferenceLayerService>();
        services.AddScoped<ITaxMapSheetService, TaxMapSheetService>();
        services.AddScoped<IBillingRuleService, BillingRuleService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<Features.PropertyIdentification.IPropertyIdentificationService, Features.PropertyIdentification.PropertyIdentificationService>();
        services.AddScoped<Features.ContentPacks.IContentPackService, Features.ContentPacks.ContentPackService>();
        services.AddScoped<Features.ContentPacks.ContentPackVersionedContent>();
        services.AddScoped<Features.PropertyIdentification.IPinService, Features.PropertyIdentification.PinService>();
        services.AddScoped<Features.Collection.IPaymentService, Features.Collection.PaymentService>();
        services.AddScoped<Features.Collection.ICollectionSetupService, Features.Collection.CollectionSetupService>();
        services.AddScoped<Features.Collection.ICollectionReportService, Features.Collection.CollectionReportService>();
        services.AddScoped<Features.Offices.IOfficeContext, Features.Offices.OfficeContext>();
        // Permissions (docs/analysis/workflow-security.md §4.1)
        services.AddScoped<Features.Security.IPermissionService, Features.Security.PermissionService>();
        services.AddScoped<Features.Security.IRolePermissionService, Features.Security.RolePermissionService>();
        services.AddScoped<Features.Offices.IOfficeService, Features.Offices.OfficeService>();
        services.AddScoped<Features.Users.IUserAccountService, Features.Users.UserAccountService>();
        services.AddScoped<Features.Offices.IApprovalDelegationService, Features.Offices.ApprovalDelegationService>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<IApprovalChainService, ApprovalChainService>();
        services.AddScoped<IFormService, FormService>();
        services.AddScoped<Features.Submissions.IApprovedDocumentIssuer, Features.Submissions.ApprovedDocumentIssuer>();
        services.AddScoped<Features.Submissions.ISubmissionService, Features.Submissions.SubmissionService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<Features.Exemptions.IExemptionService, Features.Exemptions.ExemptionService>();
        services.AddScoped<Features.AssessmentLevels.IAssessmentLevelCeilingService, Features.AssessmentLevels.AssessmentLevelCeilingService>();
        services.AddScoped<INoticeService, NoticeService>();
        services.AddScoped<IFormDataProvider, NoticeFormDataProvider>();
        services.AddScoped<Features.Notices.INoticeOfCancellationService, Features.Notices.NoticeOfCancellationService>();
        services.AddScoped<IFormDataProvider, Features.Notices.NoticeOfCancellationFormDataProvider>();
        services.AddScoped<Features.Transactions.IDiscoverySummonsService, Features.Transactions.DiscoverySummonsService>();
        services.AddScoped<IFormDataProvider, Features.Transactions.DiscoverySummonsFormDataProvider>();
        services.AddScoped<Features.MarketData.IMarketTransactionService, Features.MarketData.MarketTransactionService>();
        services.AddScoped<Features.MarketData.IMarketDataAbstractService, Features.MarketData.MarketDataAbstractService>();
        services.AddScoped<Features.MarketData.IMarketDataImportService, Features.MarketData.MarketDataImportService>();
        services.AddScoped<Features.MarketData.IMarketDataReportService, Features.MarketData.MarketDataReportService>();
        services.AddScoped<IFormDataProvider, Features.MarketData.MarketDataReportFormDataProvider>();
        services.AddScoped<IAppraisalRecordService, AppraisalRecordService>();
        services.AddScoped<Features.Smv.IAdjustmentFactorService, Features.Smv.AdjustmentFactorService>();
        services.AddScoped<Features.Smv.IBuildingCostTableService, Features.Smv.BuildingCostTableService>();
        services.AddScoped<Features.Valuation.IMachineryIndexService, Features.Valuation.MachineryIndexService>();
        services.AddScoped<Features.Valuation.IIndependentAppraisalService, Features.Valuation.IndependentAppraisalService>();
        services.AddScoped<Features.Assessments.IBackTaxService, Features.Assessments.BackTaxService>();
        services.AddScoped<Features.PropertyIdentification.IBarangayPartService, Features.PropertyIdentification.BarangayPartService>();
        services.AddScoped<Features.PropertyIdentification.ITerritorialChangeService, Features.PropertyIdentification.TerritorialChangeService>();
        services.AddScoped<Features.PropertyIdentification.TerritorialChangeJobRunner>();
        services.AddScoped<Features.Descriptions.IDescriptionService, Features.Descriptions.DescriptionService>();
        services.AddScoped<IFormDataProvider, AppraisalRecordFormDataProvider>();
        services.AddScoped<IFormDataProvider, StatementOfAccountFormDataProvider>();
        services.AddScoped<IFormDataProvider, FaasFormDataProvider>();
        services.AddScoped<IFormDataProvider, Features.Registers.RegisterFormDataProvider>();
        services.AddScoped<Features.Registers.IRegisterService, Features.Registers.RegisterService>();
        // Reports (docs/analysis/reporting.md §4.1): one class per report, run through the report service.
        services.AddScoped<Features.Reports.IReport, Features.Reports.PropertyInventoryReport>();
        foreach (var key in Enum.GetValues<Features.Reports.PropertySummaryKey>())
        {
            services.AddScoped<Features.Reports.IReport>(sp => ActivatorUtilities.CreateInstance<Features.Reports.PropertySummaryReport>(sp, key));
        }
        services.AddScoped<Features.Reports.IReport, Features.Reports.TaxDeclarationListReport>();
        services.AddScoped<Features.Reports.IReport, Features.Reports.ValueSummaryReport>();
        services.AddScoped<Features.Reports.IReport>(sp => ActivatorUtilities.CreateInstance<Features.Reports.AssessmentHistoryReport>(sp, false));
        services.AddScoped<Features.Reports.IReport>(sp => ActivatorUtilities.CreateInstance<Features.Reports.AssessmentHistoryReport>(sp, true));
        services.AddScoped<Features.Reports.IReportService, Features.Reports.ReportService>();
        services.AddScoped<Features.Reports.IRunExportService, Features.Reports.RunExportService>();
        // Dashboard (reporting.md §4.3): figures cached a minute per jurisdiction (Q8).
        services.AddMemoryCache();
        services.AddScoped<Features.Dashboard.IDashboardService, Features.Dashboard.DashboardService>();
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
        services.AddScoped<Features.GeneralRevision.IGeneralRevisionProgrammeService, Features.GeneralRevision.GeneralRevisionProgrammeService>();
        services.AddScoped<Features.GeneralRevision.IGeneralRevisionRecordsService, Features.GeneralRevision.GeneralRevisionRecordsService>();
        services.AddScoped<Features.GeneralRevision.IGeneralRevisionCompletionService, Features.GeneralRevision.GeneralRevisionCompletionService>();
        services.AddScoped<IFormDataProvider, Features.GeneralRevision.GeneralRevisionReportFormDataProvider>();
        services.AddScoped<Features.GeneralRevision.GeneralRevisionProgrammeRunner>();
        services.AddScoped<Features.Smv.ISmvPreparationService, Features.Smv.SmvPreparationService>();
        services.AddScoped<Features.Smv.ISalesAnalysisService, Features.Smv.SalesAnalysisService>();
        services.AddScoped<Features.Smv.ISmvSubClassCriteriaService, Features.Smv.SmvSubClassCriteriaService>();
        services.AddScoped<IFormDataProvider, Features.Smv.SmvFormDataProvider>();
        services.AddScoped<IFormDataProvider, Features.Smv.SalesAnalysisFormDataProvider>();
        services.AddScoped<Features.SmvSimulations.SmvSimulator>();
        services.AddScoped<Features.SmvSimulations.ISmvSimulationService, Features.SmvSimulations.SmvSimulationService>();
        services.AddScoped<Features.SmvSimulations.SmvSimulationRunner>();
        services.AddScoped<Features.SmvSimulations.IValuationTestService, Features.SmvSimulations.ValuationTestService>();
        services.AddScoped<IFormDataProvider, Features.SmvSimulations.ValuationTestFormDataProvider>();
        services.AddScoped<Features.SmvSimulations.IRevenueImpactStudyService, Features.SmvSimulations.RevenueImpactStudyService>();
        services.AddScoped<IFormDataProvider, Features.SmvSimulations.RevenueImpactFormDataProvider>();
        services.AddScoped<Features.Audit.IAuditTrailService, Features.Audit.AuditTrailService>();

        return services;
    }
}
