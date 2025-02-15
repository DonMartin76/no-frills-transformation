using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation.Plugins.Ado
{
    public abstract class AdoReader : AdoBase, ISourceReader, IRecord
    {
        public AdoReader(IContext context, string? config, string sqlQuery)
        {
            _context = context;
            _config = GetConfig(context, config);
            // You never know whether they want to use §parameters§ in the SQL...
            _sqlQuery = context.ReplaceParameters(sqlQuery);

            Initialize();
        }


        private string _config;
        private string _sqlQuery;
        private IContext _context;
        private bool _endOfStream = false;

        protected string Config { get { return _config; } }
        protected string SqlQuery { get { return _sqlQuery; } }
        protected IContext Context { get { return _context; } }

        protected virtual IDataReader SqlReader { get { throw new InvalidOperationException("Called SqlReader on abstract base class."); } }

        protected abstract void Initialize();

        public bool IsEndOfStream
        {
            get 
            { 
                return _endOfStream; 
            }
        }

        public void NextRecord()
        {
            _endOfStream = !SqlReader.Read();
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
                return SqlReader.FieldCount;
            }
        }

        private string[]? _fieldNames;
        public string[] FieldNames
        {
            get
            {
                if (null == _fieldNames)
                {
                    int count = SqlReader.FieldCount;
                    _fieldNames = new string[count];
                    for (int i = 0; i < count; ++i)
                        _fieldNames[i] = SqlReader.GetName(i);
                }
                return _fieldNames;
            }
        }

        private Dictionary<string, int>? _fieldIndexes;
        public int GetFieldIndex(string fieldName)
        {
            if (null == _fieldIndexes)
            {
                _fieldIndexes = new Dictionary<string, int>();
                for (int i = 0; i < FieldNames.Length; ++i)
                    _fieldIndexes.Add(FieldNames[i].ToLowerInvariant(), i);
            }
            fieldName = fieldName.ToLowerInvariant();
            if (_fieldIndexes.ContainsKey(fieldName))
                return _fieldIndexes[fieldName];
            return -1;
        }

        public IRecord Query(string key)
        {
            throw new NotImplementedException();
        }

        private Dictionary<int, string>? _fieldTypes;
        public string GetFieldType(int index)
        {
            if (null == _fieldTypes)
            {
                _fieldTypes = new Dictionary<int, string>();
                for (int i = 0; i < FieldCount; ++i)
                    _fieldTypes.Add(i, SqlReader.GetFieldType(i).ToString());
            }
            return _fieldTypes[index];
        }

        #region IRecord
        public string this[int index]
        {
            get
            {
                switch (GetFieldType(index)) {
                    case "System.DateTime":
                        return SqlReader.GetDateTime(index).ToString("o");
                    case "System.Byte[]":
                        byte[] bytes = new byte[SqlReader.GetBytes(index, 0, null, 0, 0)];
                        SqlReader.GetBytes(index, 0, bytes, 0, bytes.Length);
                        return "0x" + Convert.ToHexString(bytes);
                }
                return SqlReader.GetValue(index).ToString() ?? string.Empty;
            }
        }

        public string this[string fieldName]
        {
            get
            {
                int fieldIndex = GetFieldIndex(fieldName);
                if (fieldIndex < 0)
                    throw new ArgumentException("Unknown field name: " + fieldName);
                return this[fieldIndex];
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

        protected virtual void Dispose(bool disposing)
        {
            // No implementation needed here, we only have managed resources.
        }
        #endregion
    }
}
