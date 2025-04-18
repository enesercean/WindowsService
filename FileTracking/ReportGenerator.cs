using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FileTracking
{
    public interface IReportGenerator
    {
        void GenerateDailyReport(string sourceDirectory, string outputPath);
    }

    public class ReportGenerator : IReportGenerator
    {
        private readonly ILogger<ReportGenerator> _logger;

        public ReportGenerator(ILogger<ReportGenerator> logger)
        {
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GenerateDailyReport(string sourceDirectory, string outputPath)
        {
            try
            {
                _logger.LogInformation("Generating daily report from directory: {directory}", sourceDirectory);
                
                var fileInfos = GetFileInfos(sourceDirectory);
                
                if (!fileInfos.Any())
                {
                    _logger.LogWarning("No files found in the directory for reporting");
                    return;
                }
                
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11));
                        
                        page.Header().Element(ComposeHeader);
                        page.Content().Element(container => ComposeContent(container, fileInfos));
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Generated on ");
                            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            text.Span(" | Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                })
                .GeneratePdf(outputPath);
                
                _logger.LogInformation("PDF report generated successfully: {path}", outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                throw;
            }
        }
        
        private void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("File Tracking Report")
                        .FontSize(20)
                        .SemiBold();
                    
                    column.Item().Text($"Report Date: {DateTime.Now:yyyy-MM-dd}")
                        .FontSize(12);
                });
            });
        }
        
        private void ComposeContent(IContainer container, List<(string FileName, long Size)> fileInfos)
        {
            container.PaddingVertical(10).Column(column =>
            {
                column.Item().Text("Tracked Files").FontSize(14).SemiBold();
                column.Item().PaddingVertical(5);
                
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });
                    
                    table.Header(header =>
                    {
                        header.Cell().Text("#").SemiBold();
                        header.Cell().Text("File Name").SemiBold();
                        header.Cell().Text("Size").SemiBold();
                        header.Cell().Text("Last Modified").SemiBold();
                    });

                    for (int i = 0; i < fileInfos.Count; i++)
                    {
                        var (fileName, size) = fileInfos[i];
                        var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileName(fileName)));
                        
                        table.Cell().Text(i + 1);
                        table.Cell().Text(Path.GetFileName(fileName));
                        table.Cell().Text(FormatFileSize(size));
                        table.Cell().Text(fileInfo.Exists ? fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : "Unknown");
                    }
                });
                
                column.Item().PaddingVertical(10);
                column.Item().Text($"Total Files: {fileInfos.Count}");
                column.Item().Text($"Total Size: {FormatFileSize(fileInfos.Sum(f => f.Size))}");
            });
        }
        
        private List<(string FileName, long Size)> GetFileInfos(string directory)
        {
            var result = new List<(string, long)>();
            
            try
            {
                var directoryInfo = new DirectoryInfo(directory);
                if (!directoryInfo.Exists)
                {
                    _logger.LogWarning("Directory does not exist: {directory}", directory);
                    return result;
                }
                
                foreach (var file in directoryInfo.GetFiles())
                {
                    result.Add((file.FullName, file.Length));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file information from directory: {directory}", directory);
            }
            
            return result;
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n2} {suffixes[counter]}";
        }
    }
}
