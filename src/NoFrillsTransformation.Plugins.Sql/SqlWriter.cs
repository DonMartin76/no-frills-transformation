using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Sql
{
    internal class SqlWriter : ITargetWriter
    {
        private IContext _context;
        private string _fileName;
        private string[] _fieldNames;
        private int[] _fieldSizes;
        private string _config;
        private TextWriter _textWriter;
        private int _recordsWritten = 0;
        private SqlEntityConfig _entityConfig;
        private string _tableName;

        private Dictionary<string, int> _fieldDefsDict;

        public SqlWriter(IContext context, string target, string[] fieldNames, int[] fieldSizes, string? config)
        {
            this._context = context;
            var tempFileName = target.Substring(7); // Strip file://
            this._fileName = context.ResolveFileName(tempFileName, false);
            this._fieldNames = fieldNames;
            this._fieldSizes = fieldSizes;
            this._config = config ?? string.Empty;
            this._entityConfig = ReadConfig(this._config);

            _fieldDefsDict = _fieldNames.Select((name, index) => new { name, index })
                               .ToDictionary(x => x.name, x => x.index);

            _textWriter = new StreamWriter(_fileName);
            // The target table name is in the config
            if (_entityConfig.Table == null || _entityConfig.Table.Name == null)
            {
                throw new ArgumentException("Table name not found in SQL entity configuration file: " + _config);
            }
            _tableName = _entityConfig.Table.Name;
            _textWriter.WriteLine($"DROP TABLE IF EXISTS {_tableName};");
            _textWriter.WriteLine("CREATE TABLE " + _tableName + " (");
            // Copy the column names from the config file
            bool first = true;
            foreach (var col in _entityConfig.Table.Columns ?? [])
            {
                if (!first)
                {
                    _textWriter.WriteLine(",");
                }
                _textWriter.Write(col.Name + " " + col.Type);
                first = false;
            }
            _textWriter.WriteLine(");");
        }

        private SqlEntityConfig ReadConfig(string configFileName)
        {
            // this._config contains an XML file with the SqlEntityConfig structure
            // Read the file and parse it as XML according to SqlEntityConfig
            // After that, we'll use that to write the output.
            string fileName = _context.ResolveFileName(configFileName);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SqlEntityConfig));
            SqlEntityConfig? entityConfig;
            using (var fs = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
            {
                entityConfig = (SqlEntityConfig?)xmlSerializer.Deserialize(fs);
            }
            if (null == entityConfig)
            {
                throw new ArgumentException("Could not read SQL entity configuration file: " + _config);
            }
            return entityConfig;
        }

        public void WriteRecord(string[] fieldValues)
        {
            // If we're on a multiple of 1000, write a commit statement and start a new transction
            if (_recordsWritten % 1000 == 0)
            {
                if (_recordsWritten > 0)
                {
                    _textWriter.WriteLine("COMMIT;");
                }
                _textWriter.WriteLine("BEGIN TRANSACTION;");
            }
            // Take the field values and write them to the SQL file; take the field types from the
            // table entity definition into account; if it's something related to a string, put it in
            // single quotes; if it's a number, just write it as is.
            bool first = true;
            _textWriter.Write("INSERT INTO " + _tableName + " (");
            foreach (var col in _entityConfig.Table?.Columns ?? [])
            {
                if (!first)
                {
                    _textWriter.Write(", ");
                }
                _textWriter.Write(col.Name);
                first = false;
            }
            _textWriter.Write(") VALUES (");
            first = true;

            foreach (var col in _entityConfig.Table?.Columns ?? [])
            {
                if (!first)
                {
                    _textWriter.Write(", ");
                }
                
                if (null == col || null == col.Name || null == col.Type)
                {
                    _textWriter.Write("NULL");
                }
                else
                {
                    if (_fieldDefsDict.TryGetValue(col.Name, out int fieldIndex) && fieldIndex < fieldValues.Length)
                    {
                        var fieldValue = fieldValues[fieldIndex];
                        if (fieldValue == null)
                        {
                            _textWriter.Write("NULL");
                        }
                        else if (IsStringType(col.Type))
                        {
                            _textWriter.Write("N'" + fieldValue.Replace("'", "''") + "'");
                        }
                        else if (IsOtherStringLikeType(col.Type))
                        {
                            _textWriter.Write("'" + fieldValue + "'");
                        }
                        else
                        {
                            _textWriter.Write(fieldValue);
                        }
                    }
                    else
                    {
                        _textWriter.Write("NULL");
                    }
                }
                first = false;
            }
            _textWriter.WriteLine(");");
            _recordsWritten++;
        }

        private static bool IsStringType(string type)
        {
            string lowerType = type.ToLowerInvariant();
            return lowerType.StartsWith("char") || lowerType.StartsWith("varchar") || lowerType.StartsWith("text") || lowerType.StartsWith("nchar") || lowerType.StartsWith("nvarchar") || lowerType.StartsWith("ntext");
        }

        private static bool IsOtherStringLikeType(string type)
        {
            string lowerType = type.ToLowerInvariant();
            return lowerType.StartsWith("date") || lowerType.StartsWith("time") || lowerType.StartsWith("datetime") || lowerType.StartsWith("datetime2") || lowerType.StartsWith("datetimeoffset") || lowerType.StartsWith("smalldatetime") || lowerType.StartsWith("timestamp") || lowerType.StartsWith("uniqueidentifier") || lowerType.StartsWith("xml");
        }
        public int RecordsWritten
        {
            get
            {
                return _recordsWritten;
            }
        }

        public void FinishWrite()
        {
            if (_recordsWritten > 0)
            {
                _textWriter.WriteLine("COMMIT;");
            }
            _textWriter.Close();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (null != _textWriter)
                {
                    // _textWriter.Close();
                    // _xmlWriter = null;
                }
            }
            // free native resources if there are any.
            //if (nativeResource != IntPtr.Zero) 
            //{
            //    Marshal.FreeHGlobal(nativeResource);
            //    nativeResource = IntPtr.Zero;
            //}
        }
    }
}
