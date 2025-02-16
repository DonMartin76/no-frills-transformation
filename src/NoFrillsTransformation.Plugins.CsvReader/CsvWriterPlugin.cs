using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Csv
{
    public class CsvWriterPlugin : ConfigurableBase, ITargetWriter
    {
        public CsvWriterPlugin(IContext context, string target, string[] fieldNames, int[] fieldSizes, string? config)
        {
            if (null == fieldNames)
                throw new ArgumentException("Cannot create CsvWriterPlugin without field names.");

            _context = context;
            ReadConfig(config);
            CheckUtf8Setting();
            int slashIndex = target.IndexOf("//");
            _fileName = context.ResolveFileName(target.Substring(slashIndex + 2), false); // Strip <protocol>://
            _fieldNames = fieldNames;
            _fieldSizes = fieldSizes;
            _writer = new StreamWriter(_fileName, _append, _encoding);
            if (!_append && _headers)
                WriteHeaders();
        }

        private string _fileName;
        private string[] _fieldNames;
        private int[] _fieldSizes;
        private StreamWriter _writer;
        private IContext _context;

        #region Configuration
        private char _delimiter = ',';
        private string _encodingString = "UTF-8";
        private Encoding _encoding = Encoding.GetEncoding("UTF-8");
        private bool _useUtf8Bom = true;
        private bool _append = false;
        private bool _headers = true;

        protected override void SetConfig(string parameter, string configuration)
        {
            switch (parameter)
            {
                case "delim":
                    if (configuration.Length != 1)
                        throw new ArgumentException("Invalid delim setting: Delimiter must be a single character (got: '" + configuration + "')");
                    _delimiter = configuration[0];
                    break;

                case "encoding":
                    _encodingString = configuration;
                    _encoding = Encoding.GetEncoding(_encodingString);
                    break;

                case "utf8bom":
                    _useUtf8Bom = BoolFromString(configuration);
                    break;

                case "append":
                    _append = BoolFromString(configuration);
                    break;

                case "headers":
                    _headers = BoolFromString(configuration);
                    break;

                default:
                    // Do nothing with unknown parameters
                    _context.Logger.Warning("CsvWriterPlugin - Unknown parameter: " + parameter);
                    break;
            }
        }

        private void CheckUtf8Setting()
        {
            if (!_encodingString.Equals("utf-8", StringComparison.InvariantCultureIgnoreCase))
                return;

            if (_useUtf8Bom)
                return;

            // UTF-8 without BOM requires a special Encoding
            _encoding = new UTF8Encoding(false); // Will omit Byte Order Mark
        }
        #endregion

        public void WriteRecord(string[] fieldValues)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach(string value in fieldValues)
            {
                if (!first)
                {
                    sb.Append(_delimiter);
                }
                first = false;

                sb.Append(EscapeValue(value));
            }
            _writer.WriteLine(sb.ToString());
            _recordsWritten++;
        }

        private int _recordsWritten = 0;
        public int RecordsWritten
        {
            get
            {
                return _recordsWritten;
            }
        }

        private void WriteHeaders()
        {
            string headers = string.Join(_delimiter.ToString(), _fieldNames);
            _writer.WriteLine(headers);
        }

        private string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            bool containsDelimiter = value.Contains(_delimiter);
            bool containsQuote = value.Contains('"');
            bool containsNewline = value.Contains('\n');
            if (containsDelimiter
                || containsQuote
                || containsNewline)
            {
                var t = value;
                if (containsQuote)
                    t = t.Replace("\"", "\"\"");
                return string.Format("\"{0}\"", t);
            }
            // Nothing to do, just return
            return value;
        }

        public void FinishWrite()
        {
            // Doesn't do anything.
        }

        #region IDisposable
        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // NOTE: Leave out the finalizer altogether if this class doesn't 
        // own unmanaged resources itself, but leave the other methods
        // exactly as they are. 
        //~CsvReaderPlugin() 
        //{
        //    // Finalizer calls Dispose(false)
        //    Dispose(false);
        //}

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (null != _writer)
                {
                    _writer.Dispose();
                    // _writer = null;
                }
            }
            // free native resources if there are any.
            //if (nativeResource != IntPtr.Zero) 
            //{
            //    Marshal.FreeHGlobal(nativeResource);
            //    nativeResource = IntPtr.Zero;
            //}
        }
        #endregion
    }
}
