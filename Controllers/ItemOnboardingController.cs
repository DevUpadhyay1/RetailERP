using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class ItemOnboardingController : Controller
{
    private readonly ItemOnboardingService _onboarding;

    public ItemOnboardingController(ItemOnboardingService onboarding)
    {
        _onboarding = onboarding;
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
            CreateMissingLookups = true
        };
    }

    public sealed class OnboardingPageVm
    {
        public bool UpdateExisting { get; set; }
        public bool CreateMissingLookups { get; set; }
        public string? GeneralError { get; set; }
        public ItemOnboardingService.ItemImportResult? ImportResult { get; set; }
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
