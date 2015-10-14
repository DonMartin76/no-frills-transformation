﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;
using NoFrillsTransformation.Plugins.Salesforce.Config;

namespace NoFrillsTransformation.Plugins.Salesforce
{
    public class SfdcBase : IDisposable
    {
        internal SfdcBase(IContext context, SfdcConfig config)
        {
            _config = config;
            _context = context;
        }

        ~SfdcBase()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        protected SfdcConfig _config;
        protected IContext _context; 

        protected string _sdlFile;
        protected string _logFile;
        protected string _tempDir;
        private List<string> _tempFiles = new List<string>();

        protected void WaitAndLog(IContext _context, Process process)
        {
            bool fullyCompleted = false;
            string logLine = null;
            while ((logLine = process.StandardOutput.ReadLine()) != null)
            {
                var type = logLine.Length >= 24 ? logLine.Substring(24, 4).Trim().ToLowerInvariant() : "warn";
                var text = "Data Loader: " + (logLine.Length > 30 ? logLine.Substring(30) : logLine);
                switch (type)
                {
                    case "info": _context.Logger.Info(text); break;
                    case "warn": _context.Logger.Warning(text); break;
                    default: _context.Logger.Error(type + " ** " + text); break;
                }

                fullyCompleted = fullyCompleted | logLine.Contains("The operation has fully completed");
            }

            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!fullyCompleted)
            {
                _context.Logger.Error(logLine);
                _context.Logger.Error(error);
                throw new InvalidOperationException("SFDC operations via Data Loader failed. See above error message for more information.");
            }

            //_context.Logger.Info(output);
        }

        protected void GetBulkApiSettings(ref string useBulkApi, ref string bulkApiSerialMode, ref string bulkApiZipContent)
        {
            if (_context.Parameters.ContainsKey("usebulkapi"))
            {
                string bulkApiYesNo = _context.Parameters["usebulkapi"];
                bool result = false;
                if (Boolean.TryParse(bulkApiYesNo, out result))
                {
                    _context.Logger.Info("SfdcBase: UseBulkApi from parameter: " + result);
                    _config.UseBulkApi = result;
                }
                else
                {
                    _context.Logger.Warning("SfdcBase: Invalid parameter for UseBulkApi: '" + bulkApiYesNo + "'. Defaulting to 'false'.");
                    _config.UseBulkApi = false;
                }
            }
            if (_config.UseBulkApi)
            {
                _context.Logger.Info("SfdcBase: Using Bulk API.");
                useBulkApi = "<entry key=\"sfdc.useBulkApi\" value=\"true\" />";

                if (_context.Parameters.ContainsKey("bulkapiserialmode"))
                {
                    string bulkSerial = _context.Parameters["bulkapiserialmode"];
                    bool result = false;
                    if (Boolean.TryParse(bulkSerial, out result))
                    {
                        _context.Logger.Info("SfdcBase: BulkApiSerialMode from parameter: " + result);
                        _config.BulkApiSerialMode = result;
                    }
                    else
                    {
                        _context.Logger.Warning("SfdcBase: Invalid parameter for BulkApiSerialMode: '" + bulkSerial + "'. Defaulting to 'false'.");
                        _config.BulkApiSerialMode = false;
                    }
                }

                if (_config.BulkApiSerialMode)
                {
                    bulkApiSerialMode = "<entry key=\"sfdc.bulkApiSerialMode\" value=\"true\" />";
                }
            }
            if (_config.UseBulkApi)
            {
                if (_context.Parameters.ContainsKey("bulkapizipcontent"))
                {
                    string bulkZip = _context.Parameters["bulkapizipcontent"];
                    bool result = false;
                    if (Boolean.TryParse(bulkZip, out result))
                    {
                        _context.Logger.Info("SfdcBase: BulkApiZipContent from parameter: " + result);
                        _config.BulkApiZipContent = result;
                    }
                    else
                    {
                        _context.Logger.Warning("SfdcBase: Invalid parameter for BulkApiZipContent: '" + bulkZip + "'. Defaulting to 'false'.");
                        _config.BulkApiZipContent = false;
                    }
                }

                if (_config.BulkApiZipContent)
                {
                    bulkApiZipContent = "<entry key=\"sfdc.bulkApiZipContent\" value=\"true\" />";
                }
            }
        }

        protected string GetTempFileName(string suffix)
        {
            if (null == _tempDir)
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "nft-" + Path.GetRandomFileName());
                Directory.CreateDirectory(_tempDir);
            }
            string fileName = Path.Combine(_tempDir, Path.GetRandomFileName() + suffix);
            _tempFiles.Add(fileName);
            return fileName;
        }

        protected void AddTempFile(string fileName)
        {
            _tempFiles.Add(fileName);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
            }
            // free native resources if there are any.
            //if (nativeResource != IntPtr.Zero) 
            //{
            //    Marshal.FreeHGlobal(nativeResource);
            //    nativeResource = IntPtr.Zero;
            //}

            if (_config.KeepTempFiles)
            {
                _context.Logger.Warning("SfdcBase: Not deleting temporary files due to configuration setting.");
                _context.Logger.Warning("SfdcBase: Temp directory: " + _tempDir);
            }

            if (!_config.KeepTempFiles
                && _tempFiles.Count > 0)
            {
                _context.Logger.Info("SfdcBase: Cleaning up temporary files in directory: " + _tempDir);
                foreach (string file in _tempFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception e)
                    {
                        _context.Logger.Warning("Failed to delete temp file '" + file + "': " + e.Message);
                    }
                }
                _tempFiles.Clear();


                if (null != _tempDir
                    && Directory.Exists(_tempDir))
                {
                    try
                    {
                        Directory.Delete(_tempDir);
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Warning("Failed to delete temp dir: " + _tempDir + ", error: " + ex.Message);
                    }
                    _tempDir = null;
                }
                _context.Logger.Info("SfdcBase: Done cleaning up.");
            }
        }

    }
}
