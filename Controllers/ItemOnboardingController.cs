using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class ItemOnboardingController : Controller
{
    private readonly ItemOnboardingService _onboarding;
    private readonly CustomerOnboardingService _customerOnboarding;
    private readonly SupplierOnboardingService _supplierOnboarding;

    public ItemOnboardingController(
        ItemOnboardingService onboarding,
        CustomerOnboardingService customerOnboarding,
        SupplierOnboardingService supplierOnboarding)
    {
        _onboarding = onboarding;
        _customerOnboarding = customerOnboarding;
        _supplierOnboarding = supplierOnboarding;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(BuildVm());
    }

    [HttpGet]
    public IActionResult DownloadTemplate(string type = "standard")
    {
        var normalized = (type ?? "standard").Trim().ToLowerInvariant();
        var fileName = normalized == "supplier"
            ? "Supplier_Item_Catalog_Template.csv"
            : "RetailERP_Item_Import_Template.csv";

        var bytes = normalized == "supplier"
            ? _onboarding.BuildSupplierTemplateCsv()
            : _onboarding.BuildStandardTemplateCsv();

        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public IActionResult DownloadCustomerTemplate()
    {
        var bytes = _customerOnboarding.BuildTemplateCsv();
        return File(bytes, "text/csv", "RetailERP_Customer_Import_Template.csv");
    }

    [HttpGet]
    public IActionResult DownloadSupplierTemplate()
    {
        var bytes = _supplierOnboarding.BuildTemplateCsv();
        return File(bytes, "text/csv", "RetailERP_Supplier_Import_Template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCsv(IFormFile? file, bool updateExisting = true, bool createMissingLookups = true)
    {
        var vm = BuildVm();
        vm.UpdateExisting = updateExisting;
        vm.CreateMissingLookups = createMissingLookups;

        if (file is null || file.Length == 0)
        {
            vm.GeneralError = "Please choose a CSV file.";
            return View(nameof(Index), vm);
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            vm.GeneralError = "Only .csv files are supported.";
            return View(nameof(Index), vm);
        }

        await using var stream = file.OpenReadStream();
        vm.ImportResult = await _onboarding.ImportCsvAsync(
            stream,
            file.FileName,
            updateExisting,
            createMissingLookups);

        return View(nameof(Index), vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCustomers(IFormFile? file, bool updateExistingCustomers = true)
    {
        var vm = BuildVm();
        vm.CustomerUpdateExisting = updateExistingCustomers;

        if (file is null || file.Length == 0)
        {
            vm.CustomerError = "Please choose a customer CSV file.";
            return View(nameof(Index), vm);
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            vm.CustomerError = "Only .csv files are supported for customer import.";
            return View(nameof(Index), vm);
        }

        await using var stream = file.OpenReadStream();
        vm.CustomerImportResult = await _customerOnboarding.ImportCsvAsync(
            stream,
            file.FileName,
            updateExistingCustomers);

        return View(nameof(Index), vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportSuppliers(IFormFile? file, bool updateExistingSuppliers = true)
    {
        var vm = BuildVm();
        vm.SupplierUpdateExisting = updateExistingSuppliers;

        if (file is null || file.Length == 0)
        {
            vm.SupplierError = "Please choose a supplier CSV file.";
            return View(nameof(Index), vm);
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            vm.SupplierError = "Only .csv files are supported for supplier import.";
            return View(nameof(Index), vm);
        }

        await using var stream = file.OpenReadStream();
        vm.SupplierImportResult = await _supplierOnboarding.ImportCsvAsync(
            stream,
            file.FileName,
            updateExistingSuppliers);

        return View(nameof(Index), vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyStarterPack(BusinessType businessType, bool updateExistingPack = false)
    {
        var vm = BuildVm();
        vm.SelectedBusinessType = businessType;
        vm.UpdateExistingPack = updateExistingPack;
        vm.StarterPackResult = await _onboarding.ApplyStarterPackAsync(businessType, updateExistingPack);
        return View(nameof(Index), vm);
    }

    private OnboardingPageVm BuildVm()
    {
        var bizTypes = Enum.GetValues<BusinessType>()
            .Select(x => new BusinessTypeOption
            {
                Value = x,
                Label = x.ToString()
            })
            .ToList();

        return new OnboardingPageVm
        {
            BusinessTypes = bizTypes,
            SelectedBusinessType = BusinessType.Other,
            UpdateExisting = true,
            CreateMissingLookups = true,
            CustomerUpdateExisting = true,
            SupplierUpdateExisting = true
        };
    }

    public sealed class OnboardingPageVm
    {
        public bool UpdateExisting { get; set; }
        public bool CreateMissingLookups { get; set; }
        public string? GeneralError { get; set; }
        public ItemOnboardingService.ItemImportResult? ImportResult { get; set; }
        public bool CustomerUpdateExisting { get; set; }
        public bool SupplierUpdateExisting { get; set; }
        public string? CustomerError { get; set; }
        public string? SupplierError { get; set; }
        public CustomerOnboardingService.CustomerImportResult? CustomerImportResult { get; set; }
        public SupplierOnboardingService.SupplierImportResult? SupplierImportResult { get; set; }
        public List<BusinessTypeOption> BusinessTypes { get; set; } = new();
        public BusinessType SelectedBusinessType { get; set; }
        public bool UpdateExistingPack { get; set; }
        public ItemOnboardingService.StarterPackResult? StarterPackResult { get; set; }
    }

    public sealed class BusinessTypeOption
    {
        public BusinessType Value { get; set; }
        public string Label { get; set; } = "";
    }
}
