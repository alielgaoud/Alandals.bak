namespace Andalos.API.Constants
{
    public static class SettingKeys
    {
        // ===== الشركة =====
        public const string CompanyName = "Company.Name";
        public const string CompanyShortName = "Company.ShortName";
        public const string CompanyPhone = "Company.Phone";
        public const string CompanyEmail = "Company.Email";
        public const string CompanyAddress = "Company.Address";
        public const string CompanyTaxNumber = "Company.TaxNumber";

        // ===== المالية =====
        public const string Currency = "Financial.Currency";
        public const string CurrencySymbol = "Financial.CurrencySymbol";
        public const string DecimalPlaces = "Financial.DecimalPlaces";
        public const string TaxRate = "Financial.TaxRate";
        public const string TaxEnabled = "Financial.TaxEnabled";

        // ===== الإيجارات =====
        public const string RentDefaultCycle = "Rent.DefaultCycle";
        public const string RentDueDay = "Rent.DueDay";
        public const string RentGraceDays = "Rent.GraceDays";
        public const string RentLateFeeEnabled = "Rent.LateFeeEnabled";
        public const string RentLateFeePercent = "Rent.LateFeePercent";

        // ===== الترقيم التسلسلي (الأهم!) =====
        public const string ContractNumberFormat = "Numbering.ContractFormat";
        public const string ContractNumberPrefix = "Numbering.ContractPrefix";
        public const string ReceiptNumberFormat = "Numbering.ReceiptFormat";
        public const string ReceiptNumberPrefix = "Numbering.ReceiptPrefix";
        public const string MaintenanceNumberFormat = "Numbering.MaintenanceFormat";
        public const string MaintenanceNumberPrefix = "Numbering.MaintenancePrefix";
        public const string ExpenseNumberFormat = "Numbering.ExpenseFormat";
        public const string ExpenseNumberPrefix = "Numbering.ExpensePrefix";
        public const string PassCodeFormat = "Numbering.PassCodeFormat";
        public const string PassCodePrefix = "Numbering.PassCodePrefix";

        // ===== العقود =====
        public const string ContractDefaultDuration = "Contract.DefaultDurationMonths";
        public const string ContractAutoRenew = "Contract.AutoRenew";
        public const string ContractExpiryNoticeDays = "Contract.ExpiryNoticeDays";

        // ===== المحلات =====
        public const string UnitAreaUnit = "Unit.AreaUnit";

        // ===== الزوار =====
        public const string VisitorDefaultValidity = "Visitor.DefaultValidity";
        public const string VisitorEntryStart = "Visitor.EntryHoursStart";
        public const string VisitorEntryEnd = "Visitor.EntryHoursEnd";
        public const string VisitorFamilyOnly = "Visitor.FamilyOnly";

        // ===== النظام =====
        public const string SystemLanguage = "System.Language";
        public const string SystemTimeZone = "System.TimeZone";
        public const string SystemDateFormat = "System.DateFormat";
        public const string SystemSessionTimeout = "System.SessionTimeout";
        public const string SystemMaintenanceMode = "System.MaintenanceMode";

        // ===== محتوى العقد =====
        public const string ContractTemplateTitle = "Contract.TemplateTitle";
        public const string ContractTemplateIntro = "Contract.TemplateIntro";
        public const string ContractLandlordLabel = "Contract.LandlordLabel";
        public const string ContractTenantLabel = "Contract.TenantLabel";
        public const string ContractUnitSectionTitle = "Contract.UnitSectionTitle";
        public const string ContractTermsSectionTitle = "Contract.TermsSectionTitle";
        public const string ContractPaymentSectionTitle = "Contract.PaymentSectionTitle";
        public const string ContractMaintenanceSectionTitle = "Contract.MaintenanceSectionTitle";
        public const string ContractTerminationSectionTitle = "Contract.TerminationSectionTitle";
        public const string ContractClauses = "Contract.Clauses";
        public const string ContractSignatureLandlord = "Contract.SignatureLandlordLabel";
        public const string ContractSignatureTenant = "Contract.SignatureTenantLabel";
        public const string ContractWitnessLabel = "Contract.WitnessLabel";
        public const string ContractFooterNote = "Contract.FooterNote";
        public const string ContractShowWitnesses = "Contract.ShowWitnesses";
        public const string ContractShowHijriDate = "Contract.ShowHijriDate";
        public const string RefundNumberFormat = "Numbering.RefundFormat";
        public const string RefundNumberPrefix = "Numbering.RefundPrefix";

    }
}