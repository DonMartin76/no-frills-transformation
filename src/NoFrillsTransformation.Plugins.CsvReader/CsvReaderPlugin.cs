using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;
using LumenWorks.Framework.IO.Csv;
using LumenWorks.Framework.IO;
using System.IO;

namespace NoFrillsTransformation.Plugins.Csv
{
    public class CsvReaderPlugin : ConfigurableBase, ISourceReader, IRecord
    {
        public CsvReaderPlugin(IContext context, string source, string? config)
        {
            _fileName = context.ResolveFileName(source.Substring(7)); // Strip file://

            TextReader? textReader = null;
            try
            {
                ReadConfig(config);
                if (null == _encoding)
                    textReader = new StreamReader(new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                else
                    textReader = new StreamReader(new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), _encoding);
                _csvReader = new CsvReader(
                    textReader,
                    true,
                    _delimiter,
                    CsvReader.DefaultQuote,
                    CsvReader.DefaultEscape,
                    CsvReader.DefaultComment,
                    ValueTrimmingOptions.None,
                    CsvReader.DefaultBufferSize);
                ConfigureReader();
            }
            catch (Exception)
            {
                if (null != textReader)
                    textReader.Dispose();
                throw;
            }

        }

        private string _fileName;
        private LumenWorks.Framework.IO.Csv.CsvReader _csvReader;

        #region Configuration
        protected char _delimiter = ',';
        protected bool _multiline = true;
        protected string? _encodingString = null;
        protected Encoding? _encoding = null;

        protected override void SetConfig(string parameter, string configuration)
        {
            switch (parameter)
            {
                case "delim":
                    if (configuration.Length != 1)
                        throw new ArgumentException("Invalid delim setting: Delimiter must be a single character (got: '" + configuration + "')");
                    _delimiter = configuration[0];
                    break;

                case "multiline":
                    string multiTemp = configuration.ToLowerInvariant();
                    switch (multiTemp)
                    {
                        case "true":
                            _multiline = true;
                            break;
                        case "false":
                            _multiline = false;
                            break;
                        default:
                            throw new ArgumentException("Invalid configuration setting for 'multiline': '" + configuration + "'. Expected true or false.");
                    }
                    break;

                case "encoding":
                    _encodingString = configuration;
                    _encoding = Encoding.GetEncoding(_encodingString);
                    break;

                default:
                    // Do nothing for now, just ignore.
                    break;
            }
        }

        protected void ConfigureReader()
        {
            _csvReader.SupportsMultiline = _multiline;
        }
        #endregion

        #region ISourceReader
        public bool IsEndOfStream
        {
            get
            {
                return _csvReader.EndOfStream;
            }
        }

        public void NextRecord()
        {
            if (IsEndOfStream)
                return;
            _csvReader.ReadNextRecord();
        }

        public IRecord CurrentRecord
        {
            get
            {
                return this;
            }
        }
        public int FieldCount
        {
            get
            {
                return _csvReader.FieldCount;
            }
        }

        private string[]? _fieldNames = null;
        public string[] FieldNames
        {
            get
            {
                if (null == _fieldNames)
                {
                    _fieldNames = _csvReader.GetFieldHeaders();
                }
                return _fieldNames;
            }
        }
        public int GetFieldIndex(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field names for retrieving indexes must be non-empty.");
            //for (int i = 0; i < FieldNames.Length; ++i)
            //{
            //    if (fieldName.Equals(FieldNames[i], StringComparison.InvariantCultureIgnoreCase))
            //        return i;
            //}
            return _csvReader.GetFieldIndex(fieldName);
            //return -1;
        }

        public IRecord Query(string key)
        {
            throw new InvalidOperationException("The CSV Reader (CsvReaderPlugin) does not support the Query() operator.");
        }
        #endregion

        #region IRecord
        public string this[string fieldName]
        {
            get
            {
                return _csvReader[fieldName];
            }
        }

        public string this[int fieldIndex]
        {
            get
            {
                return _csvReader[fieldIndex];
            }
        }
        #endregion

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
                if (null != _csvReader)
                {
                    _csvReader.Dispose();
                    // _csvReader = null;
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
