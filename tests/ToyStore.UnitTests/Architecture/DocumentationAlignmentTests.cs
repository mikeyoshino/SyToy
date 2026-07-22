namespace ToyStore.UnitTests.Architecture;

public sealed class DocumentationAlignmentTests
{
    [Fact]
    public void AgentGuidanceRoutesCommerceWorkToApprovedSpecification()
    {
        var agents = Read("AGENTS.md");
        var skill = Read(".agents", "skills", "toy-store-development", "SKILL.md");

        Assert.Contains(
            "docs/superpowers/specs/2026-07-17-commerce-platform-design.md",
            agents,
            StringComparison.Ordinal);
        Assert.Contains("catalog", agents, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checkout", agents, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Admin", agents, StringComparison.Ordinal);
        Assert.Contains(
            "docs/superpowers/specs/2026-07-17-commerce-platform-design.md",
            skill,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DomainDocumentsDefineApprovedNoVariantCatalogModel()
    {
        var domain = Read("docs", "DOMAIN_RULES.md");
        var architecture = Read("docs", "ARCHITECTURE.md");

        Assert.Contains("Product ไม่มี variant", domain, StringComparison.Ordinal);
        Assert.Contains("`SaleType`", domain, StringComparison.Ordinal);
        Assert.Contains("`InStock`", domain, StringComparison.Ordinal);
        Assert.Contains("`PreOrder`", domain, StringComparison.Ordinal);
        Assert.Contains("`Draft`", domain, StringComparison.Ordinal);
        Assert.Contains("`Published`", domain, StringComparison.Ordinal);
        Assert.Contains("`Archived`", domain, StringComparison.Ordinal);
        Assert.Contains("`ArtToy`", domain, StringComparison.Ordinal);
        Assert.Contains("`Gundam`", domain, StringComparison.Ordinal);
        Assert.Contains("Brand", domain, StringComparison.Ordinal);
        Assert.Contains("Universe", domain, StringComparison.Ordinal);
        Assert.Contains("Character", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductVariants", architecture, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutArchitectureUsesDurableAttemptAndCreatesOrderAfterVerifiedPayment()
    {
        var domain = Read("docs", "DOMAIN_RULES.md");
        var architecture = Read("docs", "ARCHITECTURE.md");
        var checklist = Read(
            ".agents",
            "skills",
            "toy-store-development",
            "references",
            "feature-checklist.md");

        Assert.Contains("CheckoutAttempt", domain, StringComparison.Ordinal);
        Assert.Contains("CheckoutAttempt", architecture, StringComparison.Ordinal);
        Assert.Contains("BalancePaymentRequest", architecture, StringComparison.Ordinal);
        Assert.Contains("NotificationDelivery", architecture, StringComparison.Ordinal);
        Assert.Contains("IThaiAddressCatalog", architecture, StringComparison.Ordinal);
        Assert.Contains("immutable", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CheckoutAttempt", checklist, StringComparison.Ordinal);
        Assert.Contains("verified", checklist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Order", checklist, StringComparison.Ordinal);
        Assert.DoesNotContain("Create pending order", domain, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThemesKeepCompletedStorefrontAndAdminVisualLanguagesSeparate()
    {
        var design = Read("docs", "DESIGN_SPEC.md");
        var commerceSpec = Read(
            "docs",
            "superpowers",
            "specs",
            "2026-07-17-commerce-platform-design.md");

        var designStorefront = SliceBetween(design, "## 2. Design direction", "## 3. Typography");
        var designAdmin = SliceFrom(design, "## 12. Admin design system — Muted Ocean");
        var specStorefront = SliceBetween(
            commerceSpec,
            "Storefront คง visual language",
            "Admin ใช้ borderless");
        var specAdmin = SliceBetween(
            commerceSpec,
            "Admin ใช้ borderless `Muted Ocean` blue theme แยก:",
            "### Catalog list");

        Assert.Contains("monochrome", designStorefront, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lime", designStorefront, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Muted Ocean", designAdmin, StringComparison.Ordinal);
        Assert.Contains("borderless", designAdmin, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lime", specStorefront, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#DFFF29", specStorefront, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#3F91B8", specStorefront, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Muted Ocean", specAdmin, StringComparison.Ordinal);
        Assert.Contains("#3F91B8", specAdmin, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#DFFF29", specAdmin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BacklogHasApprovedExecutableMilestonesWithoutStaleInstructions()
    {
        var tasks = Read("TASKS.md");

        var milestoneNames = new[]
        {
            "M4 — Catalog foundation, reference data, media and Admin catalog shell",
            "M5 — Inventory, Product management, Storefront and In-stock cart",
            "M6 — Pre-order",
            "M7 — Checkout, Stripe and Order creation",
            "M8 — Orders, fulfillment and notifications",
            "M9 — Admin dashboard and sales reports",
            "M10 — Single-server production readiness",
            "M11 — Quality gate before launch",
        };

        foreach (var milestoneName in milestoneNames)
        {
            Assert.Contains(milestoneName, tasks, StringComparison.Ordinal);
        }

        var requiredContracts = new[]
        {
            "Admin shell",
            "Brand",
            "Universe",
            "Character",
            "ArtToy",
            "Gundam",
            "IFileStorage",
            "StockMovement",
            "anonymous In-stock cart",
            "MaxPerCustomer",
            "IThaiAddressCatalog",
            "CheckoutAttempt",
            "Stripe Embedded Checkout",
            "verified payment",
            "BalancePaymentRequest",
            "NotificationDelivery",
            "LINE Official Account",
            "Net sales today",
        };

        foreach (var requiredContract in requiredContracts)
        {
            Assert.Contains(requiredContract, tasks, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("ProductVariant", tasks, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Create pending order", tasks, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Category admin", tasks, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manual bank transfer", tasks, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sales reports and exports", tasks, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BacklogPreservesCompletedFoundationHistoryAndMilestoneOrdering()
    {
        var tasks = Read("TASKS.md");

        var completedMilestones = new[]
        {
            SliceBetween(tasks, "## M0 —", "## M1 —"),
            SliceBetween(tasks, "## M1 —", "## M2 —"),
            SliceBetween(tasks, "## M2 —", "## M3 —"),
            SliceBetween(tasks, "## M3 —", "## M4 —"),
        };

        foreach (var milestone in completedMilestones)
        {
            Assert.Contains("- [x]", milestone, StringComparison.Ordinal);
            Assert.DoesNotContain("- [ ]", milestone, StringComparison.Ordinal);
            Assert.DoesNotContain("- [-]", milestone, StringComparison.Ordinal);
        }

        AssertAppearsInOrder(
            tasks,
            "## M4 —",
            "**M4-01**",
            "**M4-02**",
            "## M5 —",
            "## M6 —",
            "## M7 —",
            "## M8 —",
            "## M9 —",
            "## M10 —",
            "## M11 —");
    }

    [Fact]
    public void PaymentSuccessGuidanceRequiresVerifiedProviderEvidenceOnly()
    {
        var skill = Read(".agents", "skills", "toy-store-development", "SKILL.md");

        Assert.DoesNotContain(
            "Verify payment with the provider or an authorized admin",
            skill,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Only verified Stripe/provider evidence may mark payment successful",
            skill,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RoadmapM4ExitStopsAtCatalogFoundation()
    {
        var roadmap = Read("docs", "ROADMAP.md");
        var m4 = SliceBetween(
            roadmap,
            "## Phase 1 — Catalog and Admin foundation (M4)",
            "## Phase 2 — Inventory, storefront catalog and In-stock cart (M5)");

        Assert.Contains(
            "Exit criteria: catalog schema/reference/media/Admin foundation พร้อม",
            m4,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Admin สร้าง relation/media/Product draft และ publish",
            m4,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BacklogDeliversInStockManagementBeforePreOrderAdminExtension()
    {
        var tasks = Read("TASKS.md");

        Assert.Contains(
            "**M5-03** Implement In-stock Product management slices",
            tasks,
            StringComparison.Ordinal);
        Assert.Contains(
            "**M5-05** Implement published In-stock Storefront catalog queries/UI",
            tasks,
            StringComparison.Ordinal);
        Assert.Contains(
            "**M6-03** Extend Product Admin for Pre-order",
            tasks,
            StringComparison.Ordinal);
        Assert.Contains(
            "**M6-05** Build Pre-order storefront and direct-checkout entry",
            tasks,
            StringComparison.Ordinal);

        var capacityPersistence = tasks.IndexOf(
            "**M6-02** Implement concurrency-safe Pre-order capacity slices",
            StringComparison.Ordinal);
        var preOrderAdmin = tasks.IndexOf(
            "**M6-03** Extend Product Admin for Pre-order",
            StringComparison.Ordinal);
        var preOrderStorefront = tasks.IndexOf(
            "**M6-05** Build Pre-order storefront and direct-checkout entry",
            StringComparison.Ordinal);
        var preOrderStorefrontTask = SliceBetween(
            tasks,
            "**M6-05** Build Pre-order storefront and direct-checkout entry",
            "Exit criteria: Pre-order");

        Assert.True(capacityPersistence >= 0 && preOrderAdmin > capacityPersistence);
        Assert.True(preOrderStorefront > capacityPersistence);
        AssertTaskDependsOn(preOrderStorefrontTask, "M5-05", "M6-04");
    }

    [Fact]
    public void NotificationDeliveryPrecedesProviderSendsAndManualRetryFollowsThem()
    {
        var tasks = Read("TASKS.md");
        var persistence = SliceBetween(tasks, "**M8-08**", "**M8-09**");
        var email = SliceBetween(tasks, "**M8-09**", "**M8-10**");
        var line = SliceBetween(tasks, "**M8-10**", "**M8-10A**");
        var retry = SliceBetween(tasks, "**M8-11**", "**M8-12**");

        Assert.Contains("NotificationDelivery", persistence, StringComparison.Ordinal);
        AssertTaskDependsOn(email, "M8-08");
        AssertTaskDependsOn(line, "M8-08");
        AssertTaskDependsOn(retry, "M8-09", "M8-10");
    }

    [Fact]
    public void DashboardSalesDependOnBothRefundFlows()
    {
        var tasks = Read("TASKS.md");
        var salesSummary = SliceBetween(tasks, "**M9-01**", "**M9-02**");

        AssertTaskDependsOn(salesSummary, "M8-03", "M8-06");
    }

    [Fact]
    public void DeliveryEstimateHasConfigurableInitialDefault()
    {
        var domain = Read("docs", "DOMAIN_RULES.md");

        Assert.Contains(
            "ค่าเริ่มต้น 2–5 วันทำการ",
            domain,
            StringComparison.Ordinal);
        Assert.Contains("Admin แก้ไขได้", domain, StringComparison.Ordinal);
        Assert.Contains("snapshot", domain, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateTuplesAlwaysUsePaymentStatusThenFulfillmentStatus()
    {
        var sources = new[]
        {
            Read("TASKS.md"),
            Read("docs", "DOMAIN_RULES.md"),
            Read(
                "docs",
                "superpowers",
                "specs",
                "2026-07-17-commerce-platform-design.md"),
        };

        foreach (var source in sources)
        {
            Assert.DoesNotContain("Cancelled + Refunded", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Cancelled + DepositForfeited", source, StringComparison.Ordinal);
        }

        Assert.Contains("Refunded + Cancelled", sources[1], StringComparison.Ordinal);
        Assert.Contains("DepositForfeited + Cancelled", sources[1], StringComparison.Ordinal);
        Assert.Contains("PaymentStatus + FulfillmentStatus", sources[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGuidanceUsesDurableCheckoutThenProviderEvidenceThenPaymentAndOrder()
    {
        var agents = SliceBetween(
            Read("AGENTS.md"),
            "## Commerce invariants",
            "## Local workflow");
        var skill = SliceBetween(
            Read(".agents", "skills", "toy-store-development", "SKILL.md"),
            "## Protect commerce flows",
            "## Work with local infrastructure");
        var domain = SliceBetween(
            Read("docs", "DOMAIN_RULES.md"),
            "## 4. Cart, address and checkout",
            "## 6. Balance payment, shipment and notifications");
        var architecture = SliceBetween(
            Read("docs", "ARCHITECTURE.md"),
            "## 5. Data and persistence",
            "## 6. Identity and authorization");

        foreach (var guidance in new[] { agents, skill, domain, architecture })
        {
            AssertScopedCheckoutSequence(guidance);
            AssertOrderCreatedOnlyAfterVerifiedEvidence(guidance);
            AssertProviderOnlyPaymentEvidence(guidance);
        }
    }

    [Fact]
    public void ActiveGuidanceKeepsCrossCuttingDeliveryConstraintsTogether()
    {
        var agents = Read("AGENTS.md");
        var skill = Read(".agents", "skills", "toy-store-development", "SKILL.md");
        var domain = Read("docs", "DOMAIN_RULES.md");
        var architecture = Read("docs", "ARCHITECTURE.md");
        var guidanceSources = new[]
        {
            agents,
            skill,
            domain,
            architecture,
        };

        foreach (var guidance in guidanceSources)
        {
            Assert.Contains("Thai", guidance, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FluentValidation", guidance, StringComparison.Ordinal);
            AssertUtcPersistenceAndBangkokDisplayDirection(guidance);
            Assert.True(
                guidance.Contains("ไม่มี variant", StringComparison.OrdinalIgnoreCase)
                || guidance.Contains("free of variants", StringComparison.OrdinalIgnoreCase));
            Assert.True(
                guidance.Contains("one Linux server", StringComparison.OrdinalIgnoreCase)
                || guidance.Contains("server เครื่องเดียว", StringComparison.OrdinalIgnoreCase));
        }

        AssertInfrastructureProhibitions(SliceFrom(agents, "## Scope discipline"));
        AssertInfrastructureProhibitions(SliceBetween(
            skill,
            "## Work with local infrastructure",
            "## Validate"));
        AssertInfrastructureProhibitions(SliceFrom(
            domain,
            "## 9. Cross-cutting delivery constraints"));
        AssertInfrastructureProhibitions(SliceBetween(
            architecture,
            "## 2. Architecture baseline",
            "## 3. Solution structure"));
    }

    [Fact]
    public void RoadmapKeepsMediaPrimitivesInM4AndInteractionUiInM5()
    {
        var roadmap = Read("docs", "ROADMAP.md");
        var m4 = SliceBetween(
            roadmap,
            "## Phase 1 — Catalog and Admin foundation (M4)",
            "## Phase 2 — Inventory, storefront catalog and In-stock cart (M5)");
        var m5 = SliceBetween(
            roadmap,
            "## Phase 2 — Inventory, storefront catalog and In-stock cart (M5)",
            "## Phase 3 — Pre-order (M6)");

        Assert.Contains("IFileStorage", m4, StringComparison.Ordinal);
        Assert.Contains("ordered media metadata", m4, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preview/reorder/primary", m4, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preview/reorder/primary", m5, StringComparison.OrdinalIgnoreCase);
    }

    private static string SliceBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);

        Assert.True(startIndex >= 0);
        Assert.True(endIndex > startIndex);

        return source[startIndex..endIndex];
    }

    private static string SliceFrom(string source, string start)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);

        Assert.True(startIndex >= 0);

        return source[startIndex..];
    }

    private static void AssertAppearsInOrder(string source, params string[] values)
    {
        var previousIndex = -1;

        foreach (var value in values)
        {
            var index = source.IndexOf(
                value,
                previousIndex + 1,
                StringComparison.OrdinalIgnoreCase);

            Assert.True(index > previousIndex, $"Expected '{value}' after index {previousIndex}.");
            previousIndex = index;
        }
    }

    private static void AssertProviderOnlyPaymentEvidence(string source)
    {
        Assert.Contains("provider", source, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            source.Contains("Admin UI action", StringComparison.OrdinalIgnoreCase)
            || source.Contains("การกดจาก Admin", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            source.Contains("not payment evidence", StringComparison.OrdinalIgnoreCase)
            || source.Contains("never payment evidence", StringComparison.OrdinalIgnoreCase)
            || source.Contains("ไม่ใช่หลักฐาน payment", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertScopedCheckoutSequence(string source)
    {
        var sequenceLine = source
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SingleOrDefault(line =>
                line.Contains("CheckoutAttempt", StringComparison.OrdinalIgnoreCase)
                && line.Contains("verified", StringComparison.OrdinalIgnoreCase)
                && line.Contains("Payment", StringComparison.OrdinalIgnoreCase)
                && line.Contains("Order", StringComparison.OrdinalIgnoreCase)
                && line.Contains("->", StringComparison.Ordinal));

        Assert.NotNull(sequenceLine);
        AssertAppearsInOrder(sequenceLine, "CheckoutAttempt", "verified", "Payment", "Order");
    }

    private static void AssertOrderCreatedOnlyAfterVerifiedEvidence(string source)
    {
        var lines = source.Split(
            '\n',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var ruleLine = lines.SingleOrDefault(line =>
            line.Contains("Order", StringComparison.OrdinalIgnoreCase)
            && line.Contains("verified", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("only after", StringComparison.OrdinalIgnoreCase)
                || line.Contains("หลัง verified", StringComparison.OrdinalIgnoreCase)));
        var beforeVerifiedRules = lines.Where(line =>
            line.Contains("Order", StringComparison.OrdinalIgnoreCase)
            && line.Contains("verified", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("before verified", StringComparison.OrdinalIgnoreCase)
                || line.Contains("ก่อน verified", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(ruleLine);
        Assert.Empty(beforeVerifiedRules);
    }

    private static void AssertInfrastructureProhibitions(string source)
    {
        var ruleLine = source
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SingleOrDefault(line =>
                line.Contains("Redis", StringComparison.OrdinalIgnoreCase)
                && line.Contains("background worker", StringComparison.OrdinalIgnoreCase)
                && line.Contains("scheduler", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ruleLine);
        Assert.True(
            ruleLine.Contains("do not", StringComparison.OrdinalIgnoreCase)
            || ruleLine.Contains("outside", StringComparison.OrdinalIgnoreCase)
            || ruleLine.Contains("ไม่ใช้", StringComparison.OrdinalIgnoreCase)
            || ruleLine.Contains("ไม่เพิ่ม", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertUtcPersistenceAndBangkokDisplayDirection(string source)
    {
        var ruleLine = source
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SingleOrDefault(line =>
                line.Contains("UTC", StringComparison.Ordinal)
                && line.Contains("th-TH", StringComparison.Ordinal)
                && line.Contains("Asia/Bangkok", StringComparison.Ordinal));

        Assert.NotNull(ruleLine);

        var clauses = ruleLine.Split(';', StringSplitOptions.TrimEntries);
        Assert.True(clauses.Length >= 2);

        var utcClause = clauses.First(clause => clause.Contains("UTC", StringComparison.Ordinal));
        var localeClause = clauses.Single(clause =>
            clause.Contains("th-TH", StringComparison.Ordinal)
            && clause.Contains("Asia/Bangkok", StringComparison.Ordinal));

        Assert.True(
            utcClause.Contains("persist", StringComparison.OrdinalIgnoreCase)
            || utcClause.Contains("storage", StringComparison.OrdinalIgnoreCase)
            || utcClause.Contains("เก็บ", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            localeClause.Contains("interpret", StringComparison.OrdinalIgnoreCase)
            || localeClause.Contains("display", StringComparison.OrdinalIgnoreCase)
            || localeClause.Contains("format", StringComparison.OrdinalIgnoreCase)
            || localeClause.Contains("UI", StringComparison.OrdinalIgnoreCase)
            || localeClause.Contains("แสดง", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertTaskDependsOn(string taskSection, params string[] dependencies)
    {
        var dependencyLine = taskSection
            .Split('\n', StringSplitOptions.TrimEntries)
            .Single(line => line.StartsWith("- Depends on:", StringComparison.Ordinal));
        var actualDependencies = dependencyLine[(dependencyLine.IndexOf(':') + 1)..]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var dependency in dependencies)
        {
            Assert.Contains(dependency, actualDependencies);
        }
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the directory containing ToyStore.sln.");
    }
}
