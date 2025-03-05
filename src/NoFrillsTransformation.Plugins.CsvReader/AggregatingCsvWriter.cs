using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NoFrillsTransformation.Interfaces;
using NoFrillsTransformation.Plugins.Csv;

namespace NoFrillsTransformation.Plugins.Csv
{
    internal class AggregatingCsvWriter : ITargetWriter
    {
        private IContext _context;
        private string _target;
        private string[] _fieldNames;
        private int[] _fieldSizes;
        private string _config;
        private int _recordsWritten = 0;

        public AggregatingCsvWriter(IContext context, string target, string[] fieldNames, int[] fieldSizes, string? config)
        {
            this._context = context;
            this._target = target;
            this._fieldNames = fieldNames;
            this._fieldSizes = fieldSizes;
            this._config = config ?? string.Empty;

            // There has to exist exactly two fields: Key and Aggregate
            if (this._fieldNames.Length != 2)
            {
                throw new ArgumentException("AggregatingCsvWriter: Exactly two fields are expected: Key and Aggregate.");
            }
        }

        private Dictionary<string, string> _records = new Dictionary<string, string>();

        public void WriteRecord(string[] fieldValues)
        {
            // If the field value 0 is not in the dictionary, add it, with the value of the field value 1.
            // If the field value 0 is in the dictionary, add the value of the field value 1 to the existing value, with a comma separating them.
            // Skip the record if the field value 0 is empty.
            if (string.IsNullOrEmpty(fieldValues[0]))
            {
                return;
            }
            if (!_records.ContainsKey(fieldValues[0]))
            {
                _records.Add(fieldValues[0], fieldValues[1]);
            }
            else
            {
                _records[fieldValues[0]] += $", {fieldValues[1]}";
            }

            _recordsWritten++;
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
            // We have everything in _records, now we need to sort and write
            // the records to the file.

            // Write everything into the file, using a CSV Writer
            using (var csvWriter = new CsvWriterPlugin(_context, _target, _fieldNames, _fieldSizes, _config))
            {
                foreach (var record in _records.OrderBy(record => record.Key))
                {
                    csvWriter.WriteRecord(new string[] { record.Key, record.Value });
                }
            }
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
                // Dispose managed resources
            }
        }
    }
}
