using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Security;
using Prime.Application.Features.Properties;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 12 step P12-5 (docs/analysis/workflow-security.md §4.5, Q16): an individual's TIN, contact, e-mail and address
/// are masked for a user without taxpayer.view-personal, in the taxpayer registry and the property's owner lists; names
/// and a corporation's details stay visible; whoever cannot see the details cannot correct them. DEMO data; rolled back.
/// </summary>
public class PersonalDataTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Masks_keep_the_shape_and_only_the_last_digits()
    {
        PersonalData.MaskTin("123-456-789-000").ShouldBe("***-***-***-000");
        PersonalData.MaskTin("123456789").ShouldBe("******789");
        PersonalData.MaskContact("+63 917 123 4567").ShouldBe("+** *** *** 4567");
        PersonalData.MaskEmail("juan.cruz@example.ph").ShouldBe("j***@example.ph");
        PersonalData.MaskEmail("no-at-sign").ShouldBe("***");
        PersonalData.MaskAddress("Purok 3, DEMO Barangay").ShouldBe(PersonalData.HiddenAddress);
        PersonalData.MaskTin(null).ShouldBeNull();
        PersonalData.MaskAddress("").ShouldBe("");
    }

    [Fact]
    public async Task A_viewer_without_the_permission_sees_an_individuals_personal_data_masked()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));

        var person = new Taxpayer
        {
            TaxpayerType = TaxpayerType.Individual, LastName = "DemoMaskCruz", FirstName = "Juan", Tin = "123-456-789-000",
            Address = "DEMO Purok 3", ContactNumber = "09171234567", Email = "juan.cruz@example.invalid",
        };
        var company = new Taxpayer
        {
            TaxpayerType = TaxpayerType.Corporation, CorporateName = "DEMO Mask Corp", Tin = "987-654-321-000", Address = "DEMO Business Park",
        };
        db.AddRange(person, company);
        db.PropertyTaxpayers.AddRange(
            new PropertyTaxpayer { PropertyId = seed.PropertyId, TaxpayerId = person.Id, Role = PropertyPartyRole.Administrator, StartDate = new DateOnly(2026, 1, 1) },
            new PropertyTaxpayer { PropertyId = seed.PropertyId, TaxpayerId = company.Id, Role = PropertyPartyRole.Administrator, StartDate = new DateOnly(2026, 1, 1) });
        var viewer = await TestSeed.UserWithRoleAsync(db, RoleCodes.ViewOnly);
        var encoder = await TestSeed.UserWithRoleAsync(db, RoleCodes.AssessmentEncoder);
        await db.SaveChangesAsync();

        var taxpayers = scope.ServiceProvider.GetRequiredService<ITaxpayerService>();
        var properties = scope.ServiceProvider.GetRequiredService<IPropertyService>();

        user.AppUserId = viewer.Id;
        var masked = (await taxpayers.GetByIdAsync(person.Id)).Value;
        (masked.PersonalDataMasked, masked.DisplayName.Contains("DemoMaskCruz")).ShouldBe((true, true));
        (masked.Tin, masked.ContactNumber, masked.Email, masked.Address)
            .ShouldBe(("***-***-***-000", "*******4567", "j***@example.invalid", PersonalData.HiddenAddress));
        var corporate = (await taxpayers.GetByIdAsync(company.Id)).Value;
        (corporate.PersonalDataMasked, corporate.Tin, corporate.Address).ShouldBe((false, "987-654-321-000", "DEMO Business Park"));
        (await taxpayers.SearchAsync(new TaxpayerSearchRequest { SearchTerm = "DemoMaskCruz" })).Value.Items.Single().Tin.ShouldBe("***-***-***-000");
        var owners = (await properties.GetProfileAsync(seed.PropertyId)).Value.Owners;
        owners.Single(o => o.TaxpayerId == person.Id).ShouldSatisfyAllConditions(
            o => o.Address.ShouldBe(PersonalData.HiddenAddress), o => o.AddressMasked.ShouldBeTrue());
        owners.Single(o => o.TaxpayerId == company.Id).Address.ShouldBe("DEMO Business Park");
        (await taxpayers.GetOwnershipHistoryAsync(seed.PropertyId)).Value.Single(o => o.TaxpayerId == person.Id).Address.ShouldBe(PersonalData.HiddenAddress);
        // A masked value is never saved back.
        (await taxpayers.UpdateDetailsAsync(person.Id, new UpdateTaxpayerDetailsRequest(masked.Tin, masked.Address, null, null, null, "DEMO")))
            .Code.ShouldBe("PERSONAL_DATA_FORBIDDEN");

        user.AppUserId = encoder.Id;
        var full = (await taxpayers.GetByIdAsync(person.Id)).Value;
        (full.PersonalDataMasked, full.Tin, full.Address, full.Email).ShouldBe((false, "123-456-789-000", "DEMO Purok 3", "juan.cruz@example.invalid"));
        (await properties.GetProfileAsync(seed.PropertyId)).Value.Owners.Single(o => o.TaxpayerId == person.Id).Address.ShouldBe("DEMO Purok 3");
    }
}
