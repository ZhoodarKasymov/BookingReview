using BookingReview.Models;

namespace BookingReview.Services.Interfaces;

public interface IExportService
{
    Task<IEnumerable<dynamic>> GetServicesAsync();

    Task<IEnumerable<dynamic>> GetWorkersAsync();

    Task<IEnumerable<dynamic>> GetCommonReportDataAsync(FilterModel filter);

    Task<IEnumerable<dynamic>> GetUserServiceReportAsync(FilterModel filter);

    Task<byte[]> GenerateExcelAsync(FilterModel filter);
    
    Task<byte[]> GeneratePdfAsync(FilterModel filter);
}