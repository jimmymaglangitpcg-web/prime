using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using Prime.Domain.Common;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.WebApi.Commands;

/// <summary>
/// <c>dotnet run --project src/Prime.WebApi -- generate-volume [properties]</c>: a province-sized DEMO data set for the
/// performance measurements of step H4 (docs/analysis/production-hardening.md §4.5, Q7). Refused unless the environment is
/// Development, the database is on this machine and its name contains "volume": the rows are history and cannot be
/// deleted (H1), so they never go into the shared dev database the tests use, nor into Supabase.
/// <para>
/// It writes, for the existing Zamboanga Sibugay municipalities and barangays: its own DEMO valuation configuration (three
/// classifications, an approved SMV covering the 16 municipalities with a land rate per barangay, building cost and
/// depreciation tables, assessment levels), then the registry: properties with a mapped parcel, a sole owner, a land
/// unit and, on three properties in five, a building unit, each unit with an approved Tax Declaration, and the create
/// audit rows the application would have written. No valuation or assessment is written: those come from the engine
/// itself, by a general revision run over the set, which is one of the measurements. Every name, number and value is
/// DEMO, not LGU data (CLAUDE.md §81).
/// </para>
/// </summary>
public static class GenerateVolumeCommand
{
    public const string Name = "generate-volume";
    private const int DefaultProperties = 250_000;
    private const int Batch = 1_000;
    private const string Marker = "DEMOVOL";
    private const string ProvinceName = "Zamboanga Sibugay";
    private static readonly DateOnly Registered = new(2024, 1, 1);
    private static readonly DateOnly SmvEffective = new(2027, 1, 1);

    public static async Task<int> RunAsync(IConfiguration configuration, IHostEnvironment environment, string[] args)
    {
        var connectionString = configuration.GetConnectionString("PrimeDb") ?? "";
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (!environment.IsDevelopment()
            || target.Host is not ("localhost" or "127.0.0.1" or "::1")
            || target.Database?.Contains("volume", StringComparison.OrdinalIgnoreCase) != true)
        {
            Console.Error.WriteLine("Refused: generate-volume runs only in Development, against a local database whose name contains \"volume\" " +
                "(set ConnectionStrings__PrimeDb). The rows cannot be deleted afterwards.");
            return 1;
        }
        var properties = args.Length > 1 && int.TryParse(args[1], out var n) && n > 0 ? n : DefaultProperties;

        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseNpgsql(connectionString, o => o.UseNetTopologySuite().CommandTimeout(600))
            .Options;
        await using var db = new PrimeDbContext(options);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        if (await db.Set<Classification>().AnyAsync(x => x.Code.StartsWith(Marker)))
        {
            Console.Error.WriteLine("Refused: this database already holds a DEMO volume set.");
            return 1;
        }
        var province = await db.Set<Province>().SingleAsync(x => x.Name == ProvinceName);
        var municipalities = await db.Set<Municipality>().Where(x => x.ProvinceId == province.Id).OrderBy(x => x.PsgcCode).ToListAsync();
        var municipalityIds = municipalities.Select(m => m.Id).ToList();
        var barangays = await db.Set<Barangay>().Where(x => municipalityIds.Contains(x.MunicipalityId) && x.IsActive)
            .OrderBy(x => x.PsgcCode).ToListAsync();
        Console.WriteLine($"{properties:N0} properties over {municipalities.Count} municipalities and {barangays.Count} barangays of {ProvinceName}.");

        var clock = Stopwatch.StartNew();
        var config = await ConfigureAsync(db, municipalities, barangays);
        Console.WriteLine($"Configuration written ({clock.Elapsed:mm\\:ss}).");
        await RegistryAsync(db, config, province, municipalities, barangays, properties, clock);
        await AuditAsync(db);
        Console.WriteLine($"Audit rows written ({clock.Elapsed:mm\\:ss}).");
        await db.Database.ExecuteSqlRawAsync("ANALYZE");
        Console.WriteLine($"Done in {clock.Elapsed:hh\\:mm\\:ss}.");
        return 0;
    }

    private sealed record Kind(Classification Classification, ActualUse Use, decimal MinArea, decimal MaxArea, int Weight);

    private sealed record Config(Kind[] Kinds, PropertyType LandType, PropertyType BuildingType, BuildingType House, StructuralType[] Structures,
        Condition Condition, OwnershipType Sole);

    /// <summary>The DEMO valuation configuration, approved. Every rate and level is invented for the measurement.</summary>
    private static async Task<Config> ConfigureAsync(PrimeDbContext db, List<Municipality> municipalities, List<Barangay> barangays)
    {
        var now = DateTimeOffset.UtcNow;
        T Lookup<T>(string code, string name) where T : LookupEntity, new() => new() { Code = $"{Marker}-{code}", Name = $"DEMO VOLUME {name}" };
        var kinds = new[]
        {
            new Kind(Lookup<Classification>("RES", "Residential"), Lookup<ActualUse>("RES", "Residential"), 120m, 1_000m, 60),
            new Kind(Lookup<Classification>("COM", "Commercial"), Lookup<ActualUse>("COM", "Commercial"), 100m, 800m, 10),
            new Kind(Lookup<Classification>("AGR", "Agricultural"), Lookup<ActualUse>("AGR", "Agricultural"), 5_000m, 60_000m, 30),
        };
        var landType = await PropertyTypeAsync(db, "LAND", "DEMO_Land");
        var buildingType = await PropertyTypeAsync(db, "BUILDING", "DEMO_Building");
        var house = Lookup<BuildingType>("HOUSE", "House");
        StructuralType[] structures = [Lookup<StructuralType>("CONC", "Concrete"), Lookup<StructuralType>("MIXED", "Mixed")];
        var condition = Lookup<Condition>("FAIR", "Fair");
        var sole = Lookup<OwnershipType>("SOLE", "Sole owner");
        db.AddRange(kinds.Select(k => k.Classification));
        db.AddRange(kinds.Select(k => k.Use));
        db.AddRange(house, condition, sole);
        db.AddRange(structures);

        var smv = new Smv
        {
            Basis = SmvBasis.Ordinance, OrdinanceNumber = $"{Marker}-SMV-2027", OrdinanceDate = new DateOnly(2026, 6, 30),
            ApprovalDate = new DateOnly(2026, 6, 30), EffectivityDate = SmvEffective, RevisionYear = 2027, Status = WorkflowStatus.Approved,
            ApprovedAt = now, Description = "DEMO VOLUME SMV for the H4 performance measurements; not an ordinance.",
            Coverage = municipalities.Select(m => new SmvCoverage { MunicipalityId = m.Id }).ToList(),
        };
        db.Add(smv);
        // A land rate per barangay and classification: the shape of a real schedule's location keys.
        var random = new Random(2027);
        foreach (var barangay in barangays)
        {
            foreach (var (kind, basis) in kinds.Zip(new[] { 1_500m, 4_000m, 25m }))
            {
                db.Add(new SmvSchedule
                {
                    SmvId = smv.Id, ClassificationId = kind.Classification.Id, ActualUseId = kind.Use.Id, PropertyTypeId = landType.Id,
                    BarangayId = barangay.Id, Unit = "per sqm", MarketValue = Math.Round(basis * (0.6m + (decimal)random.NextDouble()), 2),
                    EffectiveDate = SmvEffective, Status = WorkflowStatus.Approved, ApprovedAt = now,
                });
            }
        }
        foreach (var (structure, cost) in structures.Zip(new[] { 18_000m, 11_000m }))
        {
            db.Add(new SmvBuildingCost
            {
                SmvId = smv.Id, StructuralTypeId = structure.Id, BuildingTypeId = house.Id, CostPerSquareMetre = cost,
                LegalBasis = "DEMO", EffectiveDate = SmvEffective, Status = WorkflowStatus.Approved, ApprovedAt = now,
            });
            db.Add(new SmvDepreciationSchedule
            {
                SmvId = smv.Id, StructuralTypeId = structure.Id, Reading = DepreciationReading.YearlyWithinBand, MinimumRemainingPercent = 20m,
                Rows = [new() { Sequence = 1, FromAge = 1, ToAge = 10, Percent = 2m }, new() { Sequence = 2, FromAge = 11, Percent = 3m }],
                LegalBasis = "DEMO", EffectiveDate = SmvEffective, Status = WorkflowStatus.Approved, ApprovedAt = now,
            });
        }
        foreach (var (kind, landPercent) in kinds.Zip(new[] { 20m, 50m, 40m }))
        {
            db.Add(Level(kind, landType, 0m, null, landPercent));
            // Buildings in two brackets, so the bracket lookup is exercised at volume.
            db.Add(Level(kind, buildingType, 0m, 500_000m, 10m));
            db.Add(Level(kind, buildingType, 500_000m, null, 35m));
        }
        await SaveAsync(db);
        return new Config(kinds, landType, buildingType, house, structures, condition, sole);

        AssessmentLevel Level(Kind kind, PropertyType type, decimal lower, decimal? upper, decimal percent) => new()
        {
            OrdinanceNumber = $"{Marker}-AL-2027", OrdinanceDate = new DateOnly(2026, 6, 30), ClassificationId = kind.Classification.Id,
            ActualUseId = kind.Use.Id, PropertyTypeId = type.Id, LowerValue = lower, UpperValue = upper, AssessmentPercentage = percent,
            EffectiveDate = SmvEffective, Status = WorkflowStatus.Approved, ApprovedAt = now,
        };
    }

    /// <summary>Property types are looked up by code by the services, so an existing one is reused.</summary>
    private static async Task<PropertyType> PropertyTypeAsync(PrimeDbContext db, string code, string name)
    {
        if (await db.Set<PropertyType>().FirstOrDefaultAsync(x => x.Code == code) is { } existing)
        {
            return existing;
        }
        var created = new PropertyType { Code = code, Name = name };
        db.Add(created);
        return created;
    }

    private static readonly string[] Surnames =
    [
        "Santos", "Reyes", "Cruz", "Bautista", "Garcia", "Mendoza", "Torres", "Flores", "Ramos", "Gonzales", "Lopez", "Castillo",
        "Villanueva", "Aquino", "Rivera", "Dela Cruz", "Navarro", "Salazar", "Fernandez", "Morales", "Domingo", "Pascual", "Mercado",
        "Aguilar", "Tan", "Lim", "Alvarez", "Rosales", "Gutierrez", "Manalo", "Sarmiento", "Valdez", "Jimenez", "Hernandez", "Perez",
        "Soriano", "Abdul", "Ali", "Hassan", "Usman", "Ampatuan", "Sali", "Jamiri", "Lumaban", "Daguman", "Ebol", "Sumagang", "Pacquiao",
    ];

    private static readonly string[] GivenNames =
    [
        "Juan", "Maria", "Jose", "Ana", "Pedro", "Rosa", "Antonio", "Carmen", "Ramon", "Elena", "Roberto", "Teresa", "Manuel", "Luz",
        "Ricardo", "Gloria", "Eduardo", "Josefina", "Fernando", "Corazon", "Mohammad", "Fatima", "Abdullah", "Aisha",
    ];

    private static async Task RegistryAsync(PrimeDbContext db, Config config, Province province, List<Municipality> municipalities,
        List<Barangay> barangays, int total, Stopwatch clock)
    {
        var random = new Random(20261009);
        var geometry = new GeometryFactory(new PrecisionModel(), SpatialReference.StorageSrid);
        var municipalityIndex = municipalities.Select((m, i) => (m.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var weights = config.Kinds.Sum(k => k.Weight);
        // About seven owners for ten properties: some own several.
        var owners = Math.Max(1, total * 7 / 10);
        var ownerIds = new Guid[owners];

        // Owners first, so each property can point at one.
        for (var start = 0; start < owners; start += Batch * 5)
        {
            for (var i = start; i < Math.Min(owners, start + Batch * 5); i++)
            {
                var home = barangays[i % barangays.Count];
                var taxpayer = new Taxpayer
                {
                    TaxpayerType = TaxpayerType.Individual, LastName = $"DEMO {Surnames[random.Next(Surnames.Length)]}",
                    FirstName = $"{GivenNames[random.Next(GivenNames.Length)]} {i + 1}", Address = $"DEMO address, {home.Name}",
                    BarangayId = home.Id, MunicipalityId = home.MunicipalityId, ProvinceId = province.Id,
                };
                ownerIds[i] = taxpayer.Id;
                db.Add(taxpayer);
            }
            await SaveAsync(db);
        }
        Console.WriteLine($"{owners:N0} owners ({clock.Elapsed:mm\\:ss}).");

        var made = 0;
        var buildings = 0;
        var pending = 0;
        for (var b = 0; b < barangays.Count; b++)
        {
            var barangay = barangays[b];
            var m = municipalityIndex[barangay.MunicipalityId];
            var b3 = barangays.Take(b).Count(x => x.MunicipalityId == barangay.MunicipalityId) + 1;
            // An even share per barangay, the remainder to the first ones.
            var count = total / barangays.Count + (b < total % barangays.Count ? 1 : 0);
            // Laid out on a grid: municipalities 4 across, barangays 6 across within one, parcels 30 across within a barangay.
            var originX = 122.30 + m % 4 * 0.30 + (b3 - 1) % 6 * 0.045;
            var originY = 7.30 + m / 4 * 0.20 + (b3 - 1) / 6 * 0.030;
            TaxMapSection? section = null;
            for (var p = 0; p < count; p++)
            {
                if (p % 999 == 0)
                {
                    section = new TaxMapSection { BarangayId = barangay.Id, IndexNumber = $"{p / 999 + 1:000}", Remarks = "DEMO VOLUME section" };
                    db.Add(section);
                }
                made++;
                var parcelNumber = p % 999 + 1;
                var pin = $"DEMO-{m + 1:00}-{b3:000}-{p / 999 + 1:00}-{parcelNumber:000}";
                var roll = random.Next(weights);
                var kind = config.Kinds.First(k => (roll -= k.Weight) < 0);
                var area = Math.Round(kind.MinArea + (kind.MaxArea - kind.MinArea) * (decimal)random.NextDouble(), 0);

                var property = new PropertyEntity
                {
                    PropertyIdentificationNumber = pin, ProvinceId = province.Id, MunicipalityId = barangay.MunicipalityId, BarangayId = barangay.Id,
                    Street = $"DEMO Street {random.Next(1, 40)}", LotNumber = $"DEMO-LOT-{made}", SurveyNumber = $"DEMO-CSD-{m + 1:00}-{made}",
                    CadastralNumber = $"DEMO-CAD-{made}", TitleNumber = random.Next(3) == 0 ? null : $"DEMO-T-{made}",
                    TaxMapNumber = $"DEMO-TM-{m + 1:00}-{b3:000}", BoundaryNorth = "DEMO", BoundaryEast = "DEMO", BoundarySouth = "DEMO", BoundaryWest = "DEMO",
                };
                var x = originX + p % 30 * 0.0012;
                var y = originY + p / 30 * 0.0012;
                var square = geometry.CreatePolygon([new(x, y), new(x + 0.001, y), new(x + 0.001, y + 0.001), new(x, y + 0.001), new(x, y)]);
                var parcel = new Parcel
                {
                    PropertyId = property.Id, Geometry = geometry.CreateMultiPolygon([square]), Area = area, SurveyNumber = property.SurveyNumber,
                    LotNumber = property.LotNumber, BarangayId = barangay.Id, SectionId = section!.Id, ParcelNumber = parcelNumber,
                };
                var land = Unit(property, RpuType.Land, $"DEMO-L-{made:0000000}", null, null);
                db.AddRange(property, parcel, land.Rpu, land.Td);
                // The permanent PIN as assigned in the tax map section: what the TMCR and the section/parcel columns read.
                db.Add(new PinAssignment
                {
                    PropertyId = property.Id, Pin = pin, Kind = PinKind.Permanent, ParcelId = parcel.Id, BarangayId = barangay.Id,
                    SectionId = section!.Id, ParcelNumber = parcelNumber, AssignedAt = DateTimeOffset.UtcNow,
                });
                db.Add(new Land
                {
                    RpuId = land.Rpu.Id, PropertyId = property.Id, Area = area, ClassificationId = kind.Classification.Id, ActualUseId = kind.Use.Id,
                });
                db.Add(new PropertyTaxpayer
                {
                    PropertyId = property.Id, TaxpayerId = ownerIds[(int)((long)made * 7919 % owners)], OwnershipTypeId = config.Sole.Id,
                    OwnershipPercentage = 100m, StartDate = Registered,
                });
                if (made % 5 < 3)
                {
                    buildings++;
                    var building = Unit(property, RpuType.Building, $"DEMO-B-{made:0000000}", 1001, land.Rpu.Id);
                    var floorArea = (decimal)random.Next(30, 220);
                    var storeys = random.Next(4) == 0 ? 2 : 1;
                    var completed = random.Next(1985, 2024);
                    var record = new Building
                    {
                        RpuId = building.Rpu.Id, PropertyId = property.Id, BuildingTypeId = config.House.Id,
                        StructuralTypeId = config.Structures[random.Next(config.Structures.Length)].Id, ActualUseId = kind.Use.Id,
                        NumberOfStoreys = storeys, FloorArea = floorArea, TotalFloorArea = floorArea * storeys, YearConstructed = completed - 1,
                        YearCompleted = completed, ConditionId = config.Condition.Id,
                    };
                    record.UsePortions.Add(new BuildingUsePortion
                    {
                        BuildingId = record.Id, Sequence = 1, ClassificationId = kind.Classification.Id, ActualUseId = kind.Use.Id, FloorArea = record.TotalFloorArea,
                    });
                    db.AddRange(building.Rpu, building.Td, record);
                }
                if (++pending == Batch)
                {
                    await SaveAsync(db);
                    pending = 0;
                    if (made % (Batch * 10) == 0)
                    {
                        Console.WriteLine($"{made:N0} properties, {buildings:N0} buildings ({clock.Elapsed:mm\\:ss}).");
                    }
                }

                (RealPropertyUnit Rpu, TaxDeclaration Td) Unit(PropertyEntity owner, RpuType type, string number, int? suffix, Guid? landRpu)
                {
                    var rpu = new RealPropertyUnit
                    {
                        PropertyId = owner.Id, RpuNumber = number, RpuType = type, EffectivityDate = Registered, PinSuffix = suffix, LandRpuId = landRpu,
                        ApprovedAt = DateTimeOffset.UtcNow,
                    };
                    var td = new TaxDeclaration
                    {
                        RpuId = rpu.Id, PropertyId = owner.Id, TaxDeclarationNumber = $"DEMO-TD-{number[5..]}", EffectivityDate = Registered,
                        ClassificationId = kind.Classification.Id, ActualUseId = kind.Use.Id, AssessmentYear = Registered.Year,
                        Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow, Remarks = "DEMO VOLUME",
                    };
                    return (rpu, td);
                }
            }
        }
        await SaveAsync(db);
        Console.WriteLine($"{made:N0} properties, {buildings:N0} buildings ({clock.Elapsed:mm\\:ss}).");
    }

    /// <summary>Saves and forgets the batch. The audit interceptor, which stamps <c>CreatedAt</c>, is not on this context.</summary>
    private static async Task SaveAsync(PrimeDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in db.ChangeTracker.Entries<IAuditable>().Where(e => e.State == EntityState.Added))
        {
            entry.Entity.CreatedAt = now;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>The create rows the audit interceptor would have written (bypassed for speed), so the audit viewer is measured at volume.</summary>
    private static async Task AuditAsync(PrimeDbContext db)
    {
        foreach (var statement in AuditStatements)
        {
            await db.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private const string AuditInsert = """
        INSERT INTO "AuditLogs" ("Id", "Module", "TableName", "RecordId", "Action", "NewValue", "Timestamp")
        SELECT gen_random_uuid(), 'PropertyRegistry', 
        """;

    private static readonly string[] AuditStatements =
    [
        AuditInsert + """'Property', t."Id", 'Create', to_jsonb(t), t."CreatedAt" FROM "Property" t WHERE t."PropertyIdentificationNumber" LIKE 'DEMO-%'""",
        AuditInsert + """'RealPropertyUnit', t."Id", 'Create', to_jsonb(t), t."CreatedAt" FROM "RealPropertyUnit" t WHERE t."RpuNumber" LIKE 'DEMO-%'""",
        AuditInsert + """'TaxDeclarations', t."Id", 'Create', to_jsonb(t), t."CreatedAt" FROM "TaxDeclarations" t WHERE t."TaxDeclarationNumber" LIKE 'DEMO-TD-%'""",
        AuditInsert + """'Taxpayers', t."Id", 'Create', to_jsonb(t), t."CreatedAt" FROM "Taxpayers" t WHERE t."Address" LIKE 'DEMO address,%'""",
    ];
}
