using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FileTracking
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IReportGenerator _reportGenerator;
        private FileSystemWatcher _watcher;
        private string _sourceDirectory;
        private string _destinationDirectory;
        private string _reportDirectory;
        private Timer _reportTimer;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IReportGenerator reportGenerator)
        {
            _logger = logger;
            _configuration = configuration;
            _reportGenerator = reportGenerator;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _sourceDirectory = _configuration.GetValue<string>("FileTracking:SourceDirectory") ?? 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TrackingFile");
            
            _destinationDirectory = _configuration.GetValue<string>("FileTracking:DestinationDirectory") ?? 
                Path.Combine("D:\\", "TrackingFile");
                
            _reportDirectory = _configuration.GetValue<string>("FileTracking:ReportDirectory") ?? 
                Path.Combine("D:\\", "PDF");
            
            _logger.LogInformation("Starting file tracking service");
            _logger.LogInformation("Source directory: {SourceDirectory}", _sourceDirectory);
            _logger.LogInformation("Destination directory: {DestinationDirectory}", _destinationDirectory);
            _logger.LogInformation("Report directory: {ReportDirectory}", _reportDirectory);
            
            EnsureDirectoryExists(_sourceDirectory);
            EnsureDirectoryExists(_destinationDirectory);
            EnsureDirectoryExists(_reportDirectory);
            
            InitializeReportTimer();
            
            return base.StartAsync(cancellationToken);
        }

        private void InitializeReportTimer()
        {
            try
            {
                var now = DateTime.Now;
                var scheduledTime = new DateTime(now.Year, now.Month, now.Day, 15, 0, 0);
                
                if (now > scheduledTime)
                {
                    scheduledTime = scheduledTime.AddDays(1);
                }
                
                var timeUntilFirst = scheduledTime - now;
                _logger.LogInformation("Next report scheduled at: {time}", scheduledTime);
                
                _reportTimer = new Timer(GenerateReport, null, 
                    timeUntilFirst, 
                    TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing report timer");
            }
        }
        
        private void GenerateReport(object state)
        {
            try
            {
                _logger.LogInformation("Starting daily report generation at {time}", DateTime.Now);
                
                string reportFilePath = Path.Combine(_reportDirectory, 
                    $"FileReport_{DateTime.Now:yyyy-MM-dd}.pdf");
                
                _reportGenerator.GenerateDailyReport(_destinationDirectory, reportFilePath);
                
                _logger.LogInformation("Daily report generated successfully: {path}", reportFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily report");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            CopyExistingFiles();
            
            _watcher = new FileSystemWatcher
            {
                Path = _sourceDirectory,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Error += OnWatcherError;

            _logger.LogInformation("File watcher started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping file tracking service");
            _watcher?.Dispose();
            _reportTimer?.Dispose();
            return base.StopAsync(cancellationToken);
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation("New file detected: {FileName}", e.Name);
                
                string destinationFilePath = Path.Combine(_destinationDirectory, e.Name);
                
                var fileInfo = new FileInfo(e.FullPath);
                bool fileLocked = true;
                
                while (fileLocked)
                {
                    try
                    {
                        using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            stream.Close();
                        }
                        fileLocked = false;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(100);
                    }
                }
                
                File.Copy(e.FullPath, destinationFilePath, true);
                _logger.LogInformation("File copied successfully: {FileName}", e.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file {FileName}", e.Name);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "File watcher error");
            
            try
            {
                _watcher?.Dispose();
                
                _watcher = new FileSystemWatcher
                {
                    Path = _sourceDirectory,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileCreated;
                _watcher.Error += OnWatcherError;
                
                _logger.LogInformation("File watcher restarted after error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart file watcher");
            }
        }
        
        private void CopyExistingFiles()
        {
            try
            {
                _logger.LogInformation("Checking for existing files to copy...");
                string[] files = Directory.GetFiles(_sourceDirectory);
                
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destinationPath = Path.Combine(_destinationDirectory, fileName);
                    
                    if (!File.Exists(destinationPath))
                    {
                        File.Copy(file, destinationPath);
                        _logger.LogInformation("Copied existing file: {FileName}", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying existing files");
            }
        }
        
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation("Created directory: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create directory: {Path}", path);
                }
            }
        }
    }
}
