using System.Data;
using BookingReview.Models;
using BookingReview.Services.Interfaces;
using Dapper;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using NPOI.XSSF.UserModel;

namespace BookingReview.Services;

public class ExportService : IExportService
{
    private readonly IDbConnection _db;

    public ExportService(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<dynamic>> GetServicesAsync()
    {
        const string query = @"SELECT id, name FROM services
                    WHERE deleted IS NULL AND name <> '';";

        return await _db.QueryAsync(query);
    }

    public async Task<IEnumerable<dynamic>> GetWorkersAsync()
    {
        const string query = @"SELECT id, name FROM users
                                WHERE deleted is null AND name <> 'Администратор';";

        return await _db.QueryAsync(query);
    }

    public async Task<IEnumerable<dynamic>> GetCommonReportDataAsync(FilterModel filter)
    {
        var query = @"SELECT DISTINCT(CONCAT(cl.service_prefix, cl.number)) as 'NumberTicket',
                            ser.name as 'ServiceName',
                            us.name as 'UserName',
                            cl.stand_time as 'ClientStandTime',
                            cl.start_time as 'ClientStartTime',
                            cl.finish_time as 'ClientFinishTime',
                            TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time) as 'ClientWaitPeriod',
                            TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time) as 'UserWorkPeriod',
                            respo.name as 'ReviewName',
                            resp.comment as 'ReviewComment',
                            res.name as 'Result'
                            FROM clients cl
                            LEFT JOIN statistic st ON st.client_id = cl.id
                            LEFT JOIN users us ON us.id = cl.user_id
                            LEFT JOIN results res ON res.id = st.results_id
                            LEFT JOIN services ser ON ser.id = cl.service_id
                            LEFT JOIN response_event resp ON resp.clients_id = cl.id
                            LEFT JOIN responses respo ON respo.id = resp.response_id";

        query += GenerateWhereByFilters(filter);

        return await _db.QueryAsync(query, new
        {
            From = filter.From,
            To = filter.To,
            ServiceId = filter.ServiceId,
            UserId = filter.UserId
        });
    }

    public async Task<IEnumerable<dynamic>> GetUserServiceReportAsync(FilterModel filter)
    {
        var mainQuery = string.Empty;
        var reviewFilter = string.Empty;
        
        if (filter.From.HasValue && filter.To.HasValue)
            reviewFilter = " AND resp_date BETWEEN @From AND @To";
        else if (filter.From.HasValue)
            reviewFilter = " AND resp_date >= @From";
        else if (filter.To.HasValue)
            reviewFilter = " AND resp_date <= @To";

        var userQuery = @$"SELECT 
                            u.name as 'Name',
                            COUNT(cl.id) as 'Clients', 
                            MAX(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time)) as 'MaxWork', 
                            ROUND(AVG(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time))) as 'AvgWork', 
                            MIN(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time)) as 'MinWork', 
                            MAX(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time)) as 'MaxWait', 
                            ROUND(AVG(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time))) as 'AvgWait', 
                            MIN(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time)) as 'MinWait', 
                            (SELECT COUNT(*) FROM response_event WHERE users_id = u.id{reviewFilter}) as 'Reviews'
                            FROM users u 
                            LEFT JOIN clients cl ON cl.user_id = u.id
                            WHERE u.name <> 'Администратор' AND u.deleted IS NULL";

        var serviceQuery = @$"SELECT 
                            u.name as 'Name',
                            COUNT(cl.id) as 'Clients', 
                            MAX(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time)) as 'MaxWork', 
                            ROUND(AVG(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time))) as 'AvgWork', 
                            MIN(TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time)) as 'MinWork', 
                            MAX(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time)) as 'MaxWait', 
                            ROUND(AVG(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time))) as 'AvgWait', 
                            MIN(TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time)) as 'MinWait', 
                            (SELECT COUNT(*) FROM response_event WHERE services_id = u.id{reviewFilter}) as 'Reviews'
                            FROM services u
                            LEFT JOIN clients cl ON cl.service_id = u.id
                            WHERE u.deleted IS NULL AND u.name <> ''";

        if (filter.IsService ?? false)
            mainQuery = serviceQuery;
        else
            mainQuery = userQuery;

        mainQuery += GenerateWhereForServiceUsers(filter);

        return await _db.QueryAsync(mainQuery, new
        {
            From = filter.From,
            To = filter.To,
            ServiceId = filter.ServiceId,
            UserId = filter.UserId
        });
    }
    
    public async Task<byte[]> GenerateExcelAsync(FilterModel filter)
    {
        IEnumerable<dynamic> result;
        var workbook = new XSSFWorkbook();
        var worksheet = workbook.CreateSheet("Отчет");
        var headerRow = worksheet.CreateRow(0);
        var rowIndex = 1;
        
        if (filter.IsCommon ?? false)
        {
            result = await GetUserServiceReportAsync(filter);
            
            headerRow.CreateCell(0).SetCellValue(filter.IsService ?? false ? "Услуга" : "Работник");
            headerRow.CreateCell(1).SetCellValue("Кол-во клиентов");
            headerRow.CreateCell(2).SetCellValue("Максимальная время обслуживания");
            headerRow.CreateCell(3).SetCellValue("Средняя время обслуживания");
            headerRow.CreateCell(4).SetCellValue("Минимальная время обслуживания");
            headerRow.CreateCell(5).SetCellValue("Максимальная время ожидания");
            headerRow.CreateCell(6).SetCellValue("Средняя время ожидания");
            headerRow.CreateCell(7).SetCellValue("Минимальная время ожидания");
            headerRow.CreateCell(8).SetCellValue("Кол-во отзывов");
            
            foreach (var item in result)
            {
                var dataRow = worksheet.CreateRow(rowIndex++);
                var avgWork = item.AvgWork?.ToString() ?? "0";
                var avgWait = item.AvgWait?.ToString() ?? "0";

                dataRow.CreateCell(0).SetCellValue(item.Name);
                dataRow.CreateCell(1).SetCellValue(item.Clients ?? "0");
                dataRow.CreateCell(2).SetCellValue(item.MaxWork ?? "0");
                dataRow.CreateCell(3).SetCellValue(avgWork);
                dataRow.CreateCell(4).SetCellValue(item.MinWork ?? "0");
                dataRow.CreateCell(5).SetCellValue(item.MaxWait ?? "0");
                dataRow.CreateCell(6).SetCellValue(avgWait);
                dataRow.CreateCell(7).SetCellValue(item.MinWait ?? "0");
                dataRow.CreateCell(8).SetCellValue(item.Reviews ?? "0");
            }
            
            // Auto-size columns (optional)
            for (int col = 0; col < 9; col++)
            {
                worksheet.AutoSizeColumn(col);
            }
        }
        else
        {
            result = await GetCommonReportDataAsync(filter);
            
            headerRow.CreateCell(0).SetCellValue("Номер талона");
            headerRow.CreateCell(1).SetCellValue("Услуга");
            headerRow.CreateCell(2).SetCellValue("Работник");
            headerRow.CreateCell(3).SetCellValue("Дата принятие клиента");
            headerRow.CreateCell(4).SetCellValue("Дата начало обслуживания");
            headerRow.CreateCell(5).SetCellValue("Дата конца обслуживания");
            headerRow.CreateCell(6).SetCellValue("Время ожидания клиента (Мин)");
            headerRow.CreateCell(7).SetCellValue("Время обслуживания клиента (Мин)");
            headerRow.CreateCell(8).SetCellValue("Отзыв");
            headerRow.CreateCell(9).SetCellValue("Комментарий к отзыву");
            headerRow.CreateCell(10).SetCellValue("Результат");

            foreach (var item in result)
            {
                var dataRow = worksheet.CreateRow(rowIndex++);
                
                dataRow.CreateCell(0).SetCellValue(item.NumberTicket);
                dataRow.CreateCell(1).SetCellValue(item.ServiceName);
                dataRow.CreateCell(2).SetCellValue(item.UserName);
                dataRow.CreateCell(3).SetCellValue(item.ClientStandTime?.ToString() ?? "-");
                dataRow.CreateCell(4).SetCellValue(item.ClientStartTime?.ToString() ?? "-");
                dataRow.CreateCell(5).SetCellValue(item.ClientFinishTime?.ToString() ?? "-");
                dataRow.CreateCell(6).SetCellValue(item.ClientWaitPeriod?.ToString() ?? "0");
                dataRow.CreateCell(7).SetCellValue(item.UserWorkPeriod?.ToString() ?? "0");
                dataRow.CreateCell(8).SetCellValue(item.ReviewName ?? "");
                dataRow.CreateCell(9).SetCellValue(item.ReviewComment ?? "");
                dataRow.CreateCell(10).SetCellValue(item.Result ?? "");
            }
            
            // Auto-size columns (optional)
            for (int col = 0; col < 11; col++)
            {
                worksheet.AutoSizeColumn(col);
            }
        }

        using var ms = new MemoryStream();
        workbook.Write(ms);

        return ms.ToArray();
    }

    public async Task<byte[]> GeneratePdfAsync(FilterModel filter)
    {
        var document = new Document();
        var section = document.AddSection();
        // Set page orientation to Portrait (or Landscape if needed)
        section.PageSetup.Orientation = Orientation.Portrait;

        // Set page size to A4
        section.PageSetup.PageFormat = PageFormat.A4;

        // Adjust margins (if needed)
        section.PageSetup.LeftMargin = Unit.FromCentimeter(0.5); // Example left margin
        section.PageSetup.RightMargin = Unit.FromCentimeter(0.5); // Example right margin
        section.PageSetup.TopMargin = Unit.FromCentimeter(1); // Example top margin
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1); // Example bottom margin
        
        IEnumerable<dynamic> result;

        string headerTitle;

        if (filter.IsCommon ?? false)
            headerTitle = "Общий отчет об " + (filter.IsService ?? false ? "Услугах" : "Работниках");
        else
            headerTitle = "Отчет о работнике и клиенте";

        // Add a paragraph for the header description
        var headerDescription = section.AddParagraph(headerTitle);
        headerDescription.Format.Alignment = ParagraphAlignment.Center;
        headerDescription.Format.SpaceAfter = Unit.FromCentimeter(1);
        
        var table = section.AddTable();
        table.Borders.Width = 0.1;

        if (filter.IsCommon ?? false)
        {
            result = await GetUserServiceReportAsync(filter);
            
            for (int i = 0; i < 9; i++)
            {
                var column = table.AddColumn(Unit.FromCentimeter(2.2)); // Adjust the column width as needed
                column.Format.Alignment = ParagraphAlignment.Center;
                column.Format.Font.Size = 8.5;
            }

            var headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Alignment = ParagraphAlignment.Center;
            headerRow.Format.Font.Size = 8;
            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = Colors.LightGray; // Optional background color for headers
            
            // Add header text for each column
            GenerateHeaderRowPdf(headerRow, new[]
            {
                filter.IsService ?? false ? "Услуга" : "Работник",
                "Кол-во клиентов",
                "Максимальная время обслуживания",
                "Средняя время обслуживания",
                "Минимальная время обслуживания",
                "Максимальная время ожидания",
                "Средняя время ожидания",
                "Минимальная время ожидания",
                "Кол-во отзывов"
            });

            foreach (var item in result)
            {
                var dataRow = table.AddRow();
                dataRow.Format.Alignment = ParagraphAlignment.Center;
                
                // Add data for each column
                dataRow.Cells[0].AddParagraph(CorrectedText(item.Name));
                dataRow.Cells[1].AddParagraph(item.Clients?.ToString() ?? "0");
                dataRow.Cells[2].AddParagraph(item.MaxWork?.ToString() ?? "0");
                dataRow.Cells[3].AddParagraph(item.AvgWork?.ToString() ?? "0");
                dataRow.Cells[4].AddParagraph(item.MinWork?.ToString() ?? "0");
                dataRow.Cells[5].AddParagraph(item.MaxWait?.ToString() ?? "0");
                dataRow.Cells[6].AddParagraph(item.AvgWait?.ToString() ?? "0");
                dataRow.Cells[7].AddParagraph(item.MinWait?.ToString() ?? "0");
                dataRow.Cells[8].AddParagraph(item.Reviews?.ToString() ?? "0");
            }
        }
        else
        {
            result = await GetCommonReportDataAsync(filter);
            
            for (int i = 0; i < 11; i++)
            {
                var column = table.AddColumn(Unit.FromCentimeter(1.85)); // Adjust the column width as needed
                column.Format.Alignment = ParagraphAlignment.Center;
                column.Format.Font.Size = 7;
            }
            
            var headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Alignment = ParagraphAlignment.Center;
            headerRow.Format.Font.Size = 7;
            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = Colors.LightGray; // Optional background color for headers

            // Add header text for each column
            GenerateHeaderRowPdf(headerRow, new[]
            {
                "Номер талона",
                "Услуга",
                "Работник",
                "Дата принятие клиента",
                "Дата начало обслуживания",
                "Дата конца обслуживания",
                "Время ожидания клиента (Мин)",
                "Время обслуживания клиента (Мин)",
                "Отзыв",
                "Комментарий к отзыву",
                "Результат"
            });

            foreach (var item in result)
            {
                var dataRow = table.AddRow();
                dataRow.Format.Alignment = ParagraphAlignment.Center;

                // Add data for each column
                dataRow.Cells[0].AddParagraph(item.NumberTicket);
                dataRow.Cells[1].AddParagraph(CorrectedText(item.ServiceName));
                dataRow.Cells[2].AddParagraph(CorrectedText(item.UserName));
                dataRow.Cells[3].AddParagraph(item.ClientStandTime?.ToString() ?? "-");
                dataRow.Cells[4].AddParagraph(item.ClientStartTime?.ToString() ?? "-");
                dataRow.Cells[5].AddParagraph(item.ClientFinishTime?.ToString() ?? "-");
                dataRow.Cells[6].AddParagraph(item.ClientWaitPeriod?.ToString() ?? "0");
                dataRow.Cells[7].AddParagraph(item.UserWorkPeriod?.ToString() ?? "0");
                dataRow.Cells[8].AddParagraph(item.ReviewName ?? "");
                dataRow.Cells[9].AddParagraph(CorrectedText(item.ReviewComment ?? ""));
                dataRow.Cells[10].AddParagraph(item.Result ?? "");
            }
        }
        
        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    #region Private methods
    
    private static void GenerateHeaderRowPdf(Row headerRow, params string[] headers)
    {
        var index = 0;
        foreach (var header in headers)
        {
            headerRow.Cells[index].AddParagraph(header);
            index++;
        }
    }

    private static string CorrectedText(string longText)
    {
        var maxSubstringLength = 12;
        var currentIndex = 0;
        var substrings = new List<string>();

        while (currentIndex < longText.Length)
        {
            var substringLength = Math.Min(maxSubstringLength, longText.Length - currentIndex);

            // Find the last space within the allowed length
            var lastSpaceIndex = longText.LastIndexOf(' ', currentIndex + substringLength - 1, substringLength);
    
            if (lastSpaceIndex != -1 && lastSpaceIndex >= currentIndex)
            {
                // Split at the last space
                substrings.Add(longText.Substring(currentIndex, lastSpaceIndex - currentIndex + 1));
                currentIndex = lastSpaceIndex + 1;
            }
            else
            {
                // No space found, split at the max length
                substrings.Add(longText.Substring(currentIndex, substringLength));
                currentIndex += substringLength;
            }
        }

        return string.Join(" ", substrings);
    }

    private string GenerateWhereForServiceUsers(FilterModel filter)
    {
        var query = string.Empty;

        if (filter.From.HasValue && filter.To.HasValue)
            query = " AND cl.stand_time BETWEEN @From AND @To";
        else if (filter.From.HasValue && string.IsNullOrEmpty(query))
            query = " AND cl.stand_time >= @From";
        else if (filter.To.HasValue && string.IsNullOrEmpty(query))
            query = " AND cl.stand_time <= @To";

        if (!string.IsNullOrEmpty(filter.ServiceId))
            query += " AND u.id = @ServiceId";

        if (!string.IsNullOrEmpty(filter.UserId))
            query += " AND u.id = @UserId";

        query += " GROUP BY u.id, u.name;";

        return query;
    }

    private string GenerateWhereByFilters(FilterModel filter)
    {
        var query = string.Empty;

        if (filter.From.HasValue && filter.To.HasValue)
            query = " WHERE cl.stand_time BETWEEN @From AND @To";
        else if (filter.From.HasValue && string.IsNullOrEmpty(query))
            query = " WHERE cl.stand_time >= @From";
        else if (filter.To.HasValue && string.IsNullOrEmpty(query))
            query = " WHERE cl.stand_time <= @To";

        if (!string.IsNullOrEmpty(filter.ServiceId) && !string.IsNullOrEmpty(filter.UserId))
        {
            if (!string.IsNullOrEmpty(query))
                query += " AND ser.id = @ServiceId AND us.id = @UserId";
            else
                query = " WHERE ser.id = @ServiceId AND us.id = @UserId";
        }
        else if (!string.IsNullOrEmpty(filter.ServiceId))
        {
            if (!string.IsNullOrEmpty(query))
                query += " AND ser.id = @ServiceId";
            else
                query = " WHERE ser.id = @ServiceId";
        }
        else if (!string.IsNullOrEmpty(filter.UserId))
        {
            if (!string.IsNullOrEmpty(query))
                query += " AND us.id = @UserId";
            else
                query = " WHERE us.id = @UserId";
        }

        query += " ORDER BY cl.stand_time;";

        return query;
    }

    #endregion
}