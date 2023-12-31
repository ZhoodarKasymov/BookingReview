﻿using System.Data;
using BookingReview.Models;
using BookingReview.Services.Interfaces;
using Dapper;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace BookingReview.Services;

public class ExportService : IExportService
{
    private readonly IDbConnection _db;
    private readonly IConfiguration _cfg;

    public ExportService(IDbConnection db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<IEnumerable<dynamic>> GetServicesAsync()
    {
        const string query =
            @"SELECT s.id, s.name, s.prent_id,  sl.name as 'TranslatedName', s.deleted, sl.lang FROM services_langs sl
                        RIGHT JOIN services s ON s.id = sl.services_id
                        WHERE s.deleted IS NULL && sl.lang = 'kz_KZ'";

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
        var notUserWorkField = !string.IsNullOrEmpty(filter.UserId)
            ? "ABS(TIMESTAMPDIFF(MINUTE, LAG(cl.finish_time) OVER (ORDER BY cl.stand_time), cl.start_time)) AS 'UserNotWork',"
            : string.Empty;

        var showInputData = _cfg.GetValue<bool>("ShowInputData")
            ? "cl.input_data as 'InputData',"
            : string.Empty;

        var query = @$"SELECT DISTINCT(CONCAT(cl.service_prefix, cl.number)) as 'NumberTicket',                            
                            ser.name as 'ServiceName',
                            us.name as 'UserName',
                            cl.stand_time as 'ClientStandTime',
                            cl.start_time as 'ClientStartTime',
                            cl.finish_time as 'ClientFinishTime',
                            TIMESTAMPDIFF(MINUTE, cl.stand_time, cl.start_time) as 'ClientWaitPeriod',
                            TIMESTAMPDIFF(MINUTE, cl.start_time, cl.finish_time) as 'UserWorkPeriod',
                            {notUserWorkField}
                            {showInputData}
                            respo.name as 'ReviewName',
                            resp.comment as 'ReviewComment',
                            res.name as 'Result'
                            FROM clients cl
                            LEFT JOIN statistic st ON st.client_id = cl.id
                            INNER JOIN users us ON us.id = cl.user_id
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
                            WHERE u.deleted IS NULL AND u.name <> '' AND u.name NOT LIKE '%Окно%'";

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

            if (filter.IsRating.HasValue && filter.IsRating.Value)
            {
                var partSize = (int)Math.Ceiling(result.Count() / 3.0);
                var colors = new[]
                {
                    new XSSFColor(new[] { (byte)227, (byte)230, (byte)227 }),
                    new XSSFColor(new[] { (byte)217, (byte)236, (byte)228 }),
                    new XSSFColor(new[] { (byte)180, (byte)213, (byte)197 })
                };

                for (int partIndex = 0; partIndex < 3; partIndex++)
                {
                    var currentPart = result.Skip(partIndex * partSize).Take(partSize);

                    foreach (var item in currentPart)
                    {
                        var dataRow = worksheet.CreateRow(rowIndex++);
                        var avgWork = item.AvgWork?.ToString() ?? "0";
                        var avgWait = item.AvgWait?.ToString() ?? "0";

                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 0, item.Name);
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 1, item.Clients?.ToString() ?? "0");
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 2, item.MaxWork?.ToString() ?? "0");
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 3, avgWork);
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 4, item.MinWork?.ToString() ?? "0");
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 5, item.MaxWait?.ToString() ?? "0");
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 6, avgWait);
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 7, item.MinWait?.ToString() ?? "0");
                        CreateCellWithStyles(dataRow, workbook, colors[partIndex], 8, item.Reviews?.ToString() ?? "0");
                    }
                }
            }
            else
            {
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

            var isNewNotWorkField = !string.IsNullOrEmpty(filter.UserId);
            var showInputData = _cfg.GetValue<bool>("ShowInputData");
            
            var columnNames = new List<string>
            {
                "Номер талона",
                "Услуга",
                "Работник",
                "Дата принятие клиента",
                "Дата начало обслуживания",
                "Дата конца обслуживания",
                "Время ожидания клиента (Мин)",
                "Время обслуживания клиента (Мин)"
            };
            
            if (isNewNotWorkField)
                columnNames.Add("Время бездействия работника (Мин)");
            
            if(showInputData)
                columnNames.Add("Доп. Инфо");
            
            columnNames.AddRange(new [] { "Отзыв", "Комментарий к отзыву", "Результат" });

            for (var i = 0; i < columnNames.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(columnNames[i]);
            }

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

                if (isNewNotWorkField && showInputData)
                {
                    dataRow.CreateCell(8).SetCellValue(item.UserNotWork?.ToString() ?? "0");
                    dataRow.CreateCell(9).SetCellValue(item.InputData ?? "");
                    dataRow.CreateCell(10).SetCellValue(item.ReviewName ?? "");
                    dataRow.CreateCell(11).SetCellValue(item.ReviewComment ?? "");
                    dataRow.CreateCell(12).SetCellValue(item.Result ?? "");
                }
                else if (isNewNotWorkField)
                {
                    dataRow.CreateCell(8).SetCellValue(item.UserNotWork?.ToString() ?? "0");
                    dataRow.CreateCell(9).SetCellValue(item.ReviewName ?? "");
                    dataRow.CreateCell(10).SetCellValue(item.ReviewComment ?? "");
                    dataRow.CreateCell(11).SetCellValue(item.Result ?? "");
                }
                else if (showInputData)
                {
                    dataRow.CreateCell(8).SetCellValue(item.InputData ?? "");
                    dataRow.CreateCell(9).SetCellValue(item.ReviewName ?? "");
                    dataRow.CreateCell(10).SetCellValue(item.ReviewComment ?? "");
                    dataRow.CreateCell(11).SetCellValue(item.Result ?? "");
                }
                else
                {
                    dataRow.CreateCell(8).SetCellValue(item.ReviewName ?? "");
                    dataRow.CreateCell(9).SetCellValue(item.ReviewComment ?? "");
                    dataRow.CreateCell(10).SetCellValue(item.Result ?? "");
                }
            }

            var autoSizeHeaderLength = isNewNotWorkField ? 12 : 11;

            // Auto-size columns (optional)
            for (int col = 0; col < autoSizeHeaderLength; col++)
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

        section.PageSetup.LeftMargin = Unit.FromCentimeter(0.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(0.5);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1);

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
            headerRow.Shading.Color = Colors.White; // Optional background color for headers

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

            if (filter.IsRating.HasValue && filter.IsRating.Value)
            {
                var partSize = (int)Math.Ceiling(result.Count() / 3.0);
                var colors = new[] { new Color(227, 230, 227), new Color(217, 236, 228), new Color(180, 213, 197) };

                for (int partIndex = 0; partIndex < 3; partIndex++)
                {
                    var currentPart = result.Skip(partIndex * partSize).Take(partSize);

                    foreach (var item in currentPart)
                    {
                        var dataRow = table.AddRow();
                        dataRow.Format.LeftIndent = -3;
                        dataRow.Format.RightIndent = -3;
                        dataRow.Shading.Color = colors[partIndex];

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
            }
            else
            {
                foreach (var item in result)
                {
                    var dataRow = table.AddRow();

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
        }
        else
        {
            result = await GetCommonReportDataAsync(filter);

            var isNewNotWorkField = !string.IsNullOrEmpty(filter.UserId);
            var showInputData = _cfg.GetValue<bool>("ShowInputData");
            
            var headerTitles = new List<string>
            {
                "Номер талона",
                "Услуга",
                "Работник",
                "Дата принятия клиента",
                "Дата начала обслуживания",
                "Дата конца обслуживания",
                "Время ожидания клиента (Мин)",
                "Время обслуживания клиента (Мин)"
            };

            if (isNewNotWorkField)
                headerTitles.Add("Время бездействия работника (Мин)");
            
            if(showInputData)
                headerTitles.Add("Доп. Инфо");

            headerTitles.AddRange(new[] { "Отзыв", "Комментарий к отзыву", "Результат" });
            
            // Auto-size
            for (int i = 0; i < headerTitles.Count; i++)
            {
                var column = table.AddColumn(Unit.FromCentimeter(isNewNotWorkField || showInputData ? 1.70 : 1.85));
                column.Format.Alignment = ParagraphAlignment.Center;
                column.Format.Font.Size = 7;
            }

            var headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Alignment = ParagraphAlignment.Center;
            headerRow.Format.Font.Size = 7;
            headerRow.Format.Font.Bold = true;
            headerRow.Shading.Color = Colors.LightGray;
            
            GenerateHeaderRowPdf(headerRow, headerTitles.ToArray());

            foreach (var item in result)
            {
                var dataRow = table.AddRow();
                dataRow.Format.Alignment = ParagraphAlignment.Center;

                dataRow.Cells[0].AddParagraph(item.NumberTicket);
                dataRow.Cells[1].AddParagraph(CorrectedText(item.ServiceName));
                dataRow.Cells[2].AddParagraph(CorrectedText(item.UserName));
                dataRow.Cells[3].AddParagraph(item.ClientStandTime?.ToString() ?? "-");
                dataRow.Cells[4].AddParagraph(item.ClientStartTime?.ToString() ?? "-");
                dataRow.Cells[5].AddParagraph(item.ClientFinishTime?.ToString() ?? "-");
                dataRow.Cells[6].AddParagraph(item.ClientWaitPeriod?.ToString() ?? "0");
                dataRow.Cells[7].AddParagraph(item.UserWorkPeriod?.ToString() ?? "0");
                
                if (isNewNotWorkField && showInputData)
                {
                    dataRow.Cells[8].AddParagraph(item.UserNotWork?.ToString() ?? "0");
                    dataRow.Cells[9].AddParagraph(item.InputData ?? "");
                    dataRow.Cells[10].AddParagraph(item.ReviewName ?? "");
                    dataRow.Cells[11].AddParagraph(CorrectedText(item.ReviewComment ?? ""));
                    dataRow.Cells[12].AddParagraph(CorrectedText(item.Result ?? ""));
                }
                else if (isNewNotWorkField)
                {
                    dataRow.Cells[8].AddParagraph(item.UserNotWork?.ToString() ?? "0");
                    dataRow.Cells[9].AddParagraph(item.ReviewName ?? "");
                    dataRow.Cells[10].AddParagraph(CorrectedText(item.ReviewComment ?? ""));
                    dataRow.Cells[11].AddParagraph(CorrectedText(item.Result ?? ""));
                }
                else if (showInputData)
                {
                    dataRow.Cells[8].AddParagraph(item.InputData ?? "");
                    dataRow.Cells[9].AddParagraph(item.ReviewName ?? "");
                    dataRow.Cells[10].AddParagraph(CorrectedText(item.ReviewComment ?? ""));
                    dataRow.Cells[11].AddParagraph(CorrectedText(item.Result ?? ""));
                }
                else
                {
                    dataRow.Cells[8].AddParagraph(item.ReviewName ?? "");
                    dataRow.Cells[9].AddParagraph(CorrectedText(item.ReviewComment ?? ""));
                    dataRow.Cells[10].AddParagraph(CorrectedText(item.Result ?? ""));
                }
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
        {
            if (filter.ServiceId.Contains("[parent]"))
            {
                var parentId = filter.ServiceId.Replace("[parent]", "");
                query += $" AND u.prent_id = {parentId}";
            }
            else
                query += " AND u.id = @ServiceId";
        }

        if (!string.IsNullOrEmpty(filter.UserId))
            query += " AND u.id = @UserId";

        query += " GROUP BY u.id, u.name";

        if (filter.IsRating.HasValue && filter.IsRating.Value)
            query += " ORDER BY COUNT(cl.id) DESC";

        return query;
    }

    private string GenerateWhereByFilters(FilterModel filter)
    {
        var query = string.Empty;

        if (filter.From.HasValue && filter.To.HasValue)
        {
            var monthsDifference = Math.Abs((filter.From.Value.Year - filter.To.Value.Year) * 12 + filter.From.Value.Month - filter.To.Value.Month);

            if (monthsDifference >= 3)
                throw new Exception("Промежуток даты от и до не может быть больше 3 месяцев!");
            
            query = " WHERE cl.stand_time BETWEEN @From AND @To";
        }
        else if (filter.From.HasValue && string.IsNullOrEmpty(query))
            query = " WHERE cl.stand_time >= @From";
        else if (filter.To.HasValue && string.IsNullOrEmpty(query))
            query = " WHERE cl.stand_time <= @To";

        if (!string.IsNullOrEmpty(filter.ServiceId))
        {
            var startQuery = string.IsNullOrEmpty(query) ? " WHERE " : " AND ";

            if (filter.ServiceId.Contains("[parent]"))
            {
                var parentId = filter.ServiceId.Replace("[parent]", "");
                query += startQuery + $"ser.prent_id = {parentId}";
            }
            else
                query += startQuery + "ser.id = @ServiceId";
        }

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            if (!string.IsNullOrEmpty(query))
                query += " AND us.id = @UserId";
            else
                query = " WHERE us.id = @UserId";
        }

        query += " AND ser.name NOT LIKE '%Окно%' ORDER BY cl.stand_time;";

        return query;
    }

    private ICellStyle CreateCellStyle(XSSFWorkbook workbook, XSSFColor xssfColor)
    {
        var cellStyle = (XSSFCellStyle)workbook.CreateCellStyle();
        cellStyle.SetFillForegroundColor(xssfColor);
        cellStyle.FillPattern = FillPattern.SolidForeground;
        return cellStyle;
    }

    private void CreateCellWithStyles(IRow dataRow, XSSFWorkbook workbook, XSSFColor xssfColor, int index, string value)
    {
        var cel = dataRow.CreateCell(index);
        cel.CellStyle = CreateCellStyle(workbook, xssfColor);
        cel.SetCellValue(value);
    }

    #endregion
}