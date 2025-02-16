using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using LumenWorks.Framework.IO.Csv;
using NoFrillsTransformation.Config;
using NoFrillsTransformation.Engine;
using NoFrillsTransformation.Interfaces;

namespace NoFrillsTransformation
{
    class Program
    {
        enum ExitCodes
        {
            Success = 0,
            Failure = 1,
        }

        static int Main(string[] args)
        {
            if (VerifyArguments(args))
            {
                var parameters = ExtractParameters(args);
                if (null != parameters)
                    return (new Program()).Run(args[0], parameters);
            }
            return (int)ExitCodes.Failure;
        }

        private static bool VerifyArguments(string[] args)
        {
            if (args.Length == 0 || args[0].Equals("-help"))
            {
                OutputUsageInfo();
                return false;
            }
            if (args.Length == 1 &&
                (args[0].Equals("-version") || args[0].Equals("--version")) || args[0].Equals("-v"))
            {
                OutputVersionInfo();
                return false;
            }
            return true;
        }

        private static void OutputUsageInfo()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  nft <config file> [param1=setting1 param2=setting2...]");
        }

        private static void OutputVersionInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            // Get the informational version attribute
            var informationalVersionAttribute = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (null != informationalVersionAttribute)
            {
                // Does it contain a +?
                int plusIndex = informationalVersionAttribute.InformationalVersion.IndexOf('+');
                if (plusIndex >= 0)
                {
                    string[] parts = informationalVersionAttribute.InformationalVersion.Split('+');
                    Console.WriteLine($"NoFrillsTransformation (nft) v{parts[0]} ({parts[1]})");
                }
                else
                {
                    Console.WriteLine("NoFrillsTransformation (nft) v" + informationalVersionAttribute.InformationalVersion);
                }
            }
            else
            {
                Console.WriteLine("Warning: Could not retrieve version information.");
                Console.WriteLine("NoFrillsTransformation v<unknown>");
            }
        }

        private static Dictionary<string, string>? ExtractParameters(string[] args)
        {
            var dict = new Dictionary<string, string>();
            if (args.Length <= 1)
                return dict;
            for (int i = 1; i < args.Length; ++i)
            {
                string p = TrimQuotes(args[i]);
                int eqIndex = p.IndexOf('=');
                if (eqIndex < 0)
                {
                    Console.WriteLine("Invalid parameter setting: '" + p + "'.");
                    Console.WriteLine("Parameters have to be in format: paramName=paramValue");
                    return null;
                }
                string paramName = TrimQuotes(p.Substring(0, eqIndex));
                if (string.IsNullOrEmpty(paramName))
                {
                    Console.WriteLine("Invalid parameter name: '" + paramName + "'.");
                    return null;
                }
                string paramValue = TrimQuotes(p.Substring(eqIndex + 1));

                if (dict.ContainsKey(paramName))
                {
                    Console.WriteLine("Duplicate parameter name: '" + paramName + "'.");
                    return null;
                }

                dict[paramName] = paramValue;
            }
            return dict;
        }

        private static string TrimQuotes(string s)
        {
            if (s.StartsWith("\"") && s.EndsWith("\""))
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private int Run(string configFileName, Dictionary<string, string> parameters)
        {
            using (Context context = new Context())
            {
                try
                {
                    context.Parameters = parameters;
                    context.ConfigFileName = configFileName;
                    var configFile = ReadConfigFile(configFileName);
                    var includes = ReadIncludes(context, configFile);
                    configFile = MergeConfigFiles(configFile, includes);
                    InitMiscSettings(configFile, context);

                    // Set up MEF
                    // Load assemblies dynamically
                    // var pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
                    var pluginPath = AppContext.BaseDirectory;
                    var assemblies = Directory.GetFiles(pluginPath, "*.dll")
                        .Select(Assembly.LoadFrom)
                        .ToArray();

                    var configuration = new ContainerConfiguration().WithAssemblies(assemblies);

                    //         var catalog = new DirectoryCatalog(".");
                    // var container = new CompositionContainer(catalog);
                    using (var container = configuration.CreateContainer())
                    {
                        var loggerFactory = new LoggerFactory(container);
                        var readerFactory = new ReaderFactory(container);
                        var writerFactory = new WriterFactory(container);
                        var operatorFactory = new OperatorFactory(container);
                        var transformerFactory = new TransformerFactory(container);

                        ResolveParameters(context);
                        InitLogger(configFile, context, loggerFactory);
                        LogParameters(context);
                        ReplaceParameters(configFile, context);
                        context.Logger.Info("Read configuration file: " + configFileName);

                        InitOperators(configFile, context, operatorFactory);
                        InitLookupMaps(configFile, context, readerFactory);
                        InitCustomOperators(configFile, context);//, operatorFactory);
                        InitTransformer(configFile, context, transformerFactory);

                        LogOperators(context);

                        ReadFilters(configFile, context);
                        ReadMappings(configFile, context);
                        ReadFilterMappings(configFile, context);
                        ReadSources(context, configFile, readerFactory);

                        // Do we use "useSource" in the field mappings?
                        ReadSourceMappings(configFile, context);
                        ReadSourceFilterMappings(configFile, context);

                        if (null != configFile.OutputFields)
                        {
                            if (configFile.OutputFields.Value)
                                return OutputSourceFieldsToConsole(context, configFile.OutputFields.NoSizes);
                        }
                        if (null == configFile.Target || null == configFile.Target.Uri)
                            throw new ArgumentException("No target defined in configuration file.");

                        context.TargetWriter = writerFactory.CreateWriter(context, configFile.Target.Uri, context.TargetFields, configFile.Target.Config);
                        if (null != configFile.FilterTarget)
                        {
                            if (null == context.FilterTargetFields)
                            {
                                context.Logger.Warning("Creating a FilterTarget without explicit FilterField definitions. Using the default fields.");
                                context.FilterTargetFields = context.TargetFields;
                            }
                            context.FilterTargetWriter = writerFactory.CreateWriter(context, configFile.FilterTarget.Uri, context.FilterTargetFields, configFile.FilterTarget.Config);
                        }

                        Process(context);

                        // Explicitly dispose readers and writers to have control
                        // on when these resources are released. If something fails,
                        // this is done in the Dispose() method of Context.

                        foreach (var reader in context.SourceReaders)
                            reader.Dispose();
                        context.TargetWriter.Dispose();

                        context.Logger.Info("Operation finished successfully.");
                        Console.WriteLine("Operation finished successfully.");

                        return (int)ExitCodes.Success;
                    }
                }
                catch (Exception ex)
                {
                    if (null != context.Logger && !(context.Logger is DummyLogger))
                        context.Logger.Error("Operation failed: " + ex.Message);
                    Console.Error.WriteLine("Operation failed: " + ex.Message);
                }
                return (int)ExitCodes.Failure;
            }
        }

        private void InitMiscSettings(ConfigFileXml configFile, Context context)
        {
            if (configFile.ProgressTick > 0)
                context.ProgressTick = configFile.ProgressTick;
            else if (configFile.ProgressTick == 0)
                context.ProgressTick = 1000; // Default
            else
                context.ProgressTick = int.MaxValue; // Don't tick
        }

        private static void ResolveParameters(Context context)
        {
            string[] keys = context.Parameters.Keys.ToArray();
            foreach (string key in keys)
            {
                var value = context.Parameters[key];
                if (!value.StartsWith("@"))
                    continue;

                var fileName = context.ResolveFileName(value.Substring(1)); // strip @
                context.Parameters[key] = File.ReadAllText(fileName);
            }
        }

        private static void ReadSources(Context context, ConfigFileXml configFile, ReaderFactory readerFactory)
        {
            var sourceList = new List<ISourceReader>();
            var sourceFiles = new HashSet<string>();
            if (null != configFile.Source
                && null != configFile.Sources
                && configFile.Sources.Length > 0)
                throw new ArgumentException("Configuration error: You can't both define the <Source> and <Sources> tag.");

            if (null != configFile.Source)
            {
                var thisSource = configFile.Source;
                AddSources(context, readerFactory, sourceList, sourceFiles, thisSource);
            }

            if (null != configFile.Sources
                && configFile.Sources.Length > 0)
            {
                for (int i = 0; i < configFile.Sources.Length; ++i)
                {
                    AddSources(context, readerFactory, sourceList, sourceFiles, configFile.Sources[i]);
                }
            }

            context.SourceReaders = sourceList.ToArray();

            if (context.SourceReaders.Length <= 0)
                throw new ArgumentException("No Sources were identified.");
        }

        private static void AddSources(Context context, ReaderFactory readerFactory, List<ISourceReader> sourceList, HashSet<string> sourceFiles, SourceTargetXml thisSource)
        {
            // Wildcards in Source?
            if (thisSource.Uri.StartsWith("file://")
                && (thisSource.Uri.Contains("*") || thisSource.Uri.Contains("?")))
            {
                string sourceFile = thisSource.Uri.Substring(7); // strip file://
                context.Logger.Info("Detected wildcards in file name (" + sourceFile + ").");
                string? path = Path.GetDirectoryName(sourceFile);
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("No path found in source file name: " + sourceFile);
                string resolvedPath = context.ResolveFileName(path, false);
                string fileName = Path.GetFileName(sourceFile);
                foreach (string sourceFileName in Directory.EnumerateFiles(resolvedPath, fileName))
                {
                    if (sourceFiles.Contains(sourceFileName))
                        continue;
                    context.Logger.Info("Creating reader for: " + sourceFileName);
                    sourceList.Add(readerFactory.CreateReader(context, "file://" + sourceFileName, thisSource.Config));
                    sourceFiles.Add(sourceFileName);
                }
            }
            else
            {
                sourceList.Add(readerFactory.CreateReader(context, thisSource.Uri, thisSource.Config));
            }
        }


        private int OutputSourceFieldsToConsole(Context context, bool noSizes)
        {
            try
            {
                context.SourceReader = context.SourceReaders[0];

                int[] sizes = new int[context.SourceReader.FieldCount];
                for (int i = 0; i < sizes.Length; ++i)
                    sizes[i] = 0;
                int maxCount = 100;

                if (!noSizes)
                {
                    while (!context.SourceReader.IsEndOfStream
                        && context.SourceRecordsRead < maxCount)
                    {
                        context.SourceReader.NextRecord();
                        context.SourceRecordsRead++;

                        var rec = context.SourceReader.CurrentRecord;
                        for (int i = 0; i < sizes.Length; ++i)
                        {
                            int l = rec[i].Length;
                            if (l > sizes[i])
                                sizes[i] = l;
                        }
                    }
                    for (int i = 0; i < sizes.Length; ++i)
                    {
                        sizes[i] = ((sizes[i] + 10) / 10) * 10;
                    }
                }

                Console.WriteLine("  <Fields>");

                for (int i = 0; i < sizes.Length; ++i)
                {
                    if (!noSizes)
                        Console.WriteLine("    <Field name=\"{0}\" maxSize=\"{1}\">${0}</Field>", context.SourceReader.FieldNames[i], sizes[i]);
                    else
                        Console.WriteLine("    <Field name=\"{0}\">${0}</Field>", context.SourceReader.FieldNames[i]);
                }
                Console.WriteLine("  </Fields>");

                return (int)ExitCodes.Success;
            }
            finally
            {
                // context.SourceReader = null;
            }
        }

        private void LogOperators(Context context)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var op in context.Operators.Keys)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(op);
            }
            context.Logger.Info("Available operators: " + sb.ToString());
        }

        private void InitLogger(ConfigFileXml configFile, Context context, LoggerFactory loggerFactory)
        {
            bool usedDefaultLogger = false;
            if (null == configFile.Logger)
            {
                configFile.Logger = new LoggerXml { LogType = "std", LogLevel = "info" };
                usedDefaultLogger = true;
            }

            if (null == configFile.Logger.LogLevel)
                configFile.Logger.LogLevel = "info";
            if (null == configFile.Logger.LogType)
                configFile.Logger.LogType = "std";

            LogLevel logLevel = LogLevel.Info;
            bool unknownLogLevel = false;
            switch (configFile.Logger.LogLevel.ToLowerInvariant())
            {
                case "info": logLevel = LogLevel.Info; break;
                case "warn":
                case "warning": logLevel = LogLevel.Warning; break;
                case "error": logLevel = LogLevel.Error; break;

                default:
                    unknownLogLevel = true;
                    break;
            }

            context.Logger = loggerFactory.CreateLogger(configFile.Logger.LogType, logLevel, configFile.Logger?.Config ?? string.Empty);
            if (usedDefaultLogger)
                context.Logger.Info("Using default stdout logger (type 'std').");
            if (unknownLogLevel)
                context.Logger.Warning("Unknown log level '" + (configFile.Logger?.LogLevel ?? "<unknown>") + "', assuming 'info'.");
        }

        private void LogParameters(Context context)
        {
            foreach (string key in context.Parameters.Keys)
            {
                context.Logger.Info("Parameter: " + key + "=" + context.Parameters[key]);
            }
        }

        private void ReplaceParameters(ConfigFileXml config, Context context)
        {
            if (null != config.Source)
            {
                config.Source.Uri = context.ReplaceParameters(config.Source.Uri);
                config.Source.Config = context.ReplaceParameters(config.Source.Config);
            }
            if (null != config.Sources)
            {
                foreach (var source in config.Sources)
                {
                    source.Uri = context.ReplaceParameters(source.Uri);
                    source.Config = context.ReplaceParameters(source.Config);
                }
            }
            if (null != config.Target)
            {
                config.Target.Uri = context.ReplaceParameters(config.Target.Uri);
                config.Target.Config = context.ReplaceParameters(config.Target.Config);
            }
            if (null != config.SourceTransform)
            {
                if (null != config.SourceTransform.Transform)
                {
                    config.SourceTransform.Transform.Config = context.ReplaceParameters(config.SourceTransform.Transform.Config);
                    config.SourceTransform.Transform.Uri = context.ReplaceParameters(config.SourceTransform.Transform.Uri);
                }
                if (null != config.SourceTransform.Parameters)
                {
                    foreach (var parameter in config.SourceTransform.Parameters)
                    {
                        parameter.FunctionString = context.ReplaceParameters(parameter.FunctionString);
                    }
                }
                if (null != config.SourceTransform.Settings)
                {
                    foreach (var setting in config.SourceTransform.Settings)
                    {
                        setting.Setting = context.ReplaceParameters(setting.Setting);
                    }
                }
            }
            if (null != config.FilterTarget)
            {
                config.FilterTarget.Uri = context.ReplaceParameters(config.FilterTarget.Uri);
                config.FilterTarget.Config = context.ReplaceParameters(config.FilterTarget.Config);
            }

            if (null != config.LookupMaps)
            {
                foreach (var lookup in config.LookupMaps)
                {
                    if (null != lookup.Source)
                    {
                        lookup.Source.Uri = context.ReplaceParameters(lookup.Source.Uri);
                        lookup.Source.Config = context.ReplaceParameters(lookup.Source.Config);
                    }
                }
            }
        }


        private void InitOperators(ConfigFileXml config, Context context, OperatorFactory operatorFactory)
        {
            //var opFactory = new OperatorFactory(container);
            foreach (var op in operatorFactory.Operators)
            {
                if (context.Operators.ContainsKey(op.Name))
                    throw new InvalidOperationException("An operator with the name '" + op.Name + "' is already defined. Operators' names must be unique.");
                context.Operators[op.Name] = op;
            }

            // Do we have operator configurations?
            if (null != config.OperatorConfigs)
            {
                foreach (var opConfig in config.OperatorConfigs)
                {
                    if (null == opConfig.Name)
                        throw new InvalidOperationException("Operator configuration without name found.");
                    string opName = opConfig.Name.ToLowerInvariant();
                    if (!context.Operators.ContainsKey(opName))
                        throw new InvalidOperationException("Configuration was passed in config XML for unknown operator '" + opConfig.Name + ". Are your plugins in the same folder as the executable?");
                    context.Operators[opName].Configure(opConfig.Config);
                }
            }

            context.Logger.Info("Initialized operators.");
        }

        private void InitCustomOperators(ConfigFileXml configFile, Context context)
        {
            try
            {
                if (null == configFile.CustomOperators)
                    return;

                foreach (var op in configFile.CustomOperators)
                {
                    if (string.IsNullOrWhiteSpace(op.ReturnType))
                        throw new ArgumentException("Custom operator without return type found.");
                    var returnType = ExpressionParser.StringToType(op.ReturnType);
                    if (string.IsNullOrWhiteSpace(op.Name))
                        throw new ArgumentException("Custom operator without name found.");
                    var name = op.Name.ToLowerInvariant();
                    var paramNameList = new List<string>();
                    var paramTypeList = new List<ParamType>();
                    if (null != op.Parameters)
                    {
                        foreach (var param in op.Parameters)
                        {
                            if (string.IsNullOrWhiteSpace(param.Name))
                                throw new ArgumentException("Custom operator parameter without name found.");
                            var paramType = ExpressionParser.StringToType(param.Type);
                            if (paramType == ParamType.Undefined)
                                throw new ArgumentException("Custom operator parameter without or with unknown type found.");
                            paramNameList.Add(param.Name.ToLowerInvariant());
                            paramTypeList.Add(paramType);
                        }
                    }
                    if (paramNameList.Count != op.ParamCount)
                        throw new ArgumentException("Custom operator paramCount attribute does not match actual parameter count: " + op.ParamCount + " vs. " + paramNameList.Count);

                    if (context.Operators.ContainsKey(name))
                        throw new ArgumentException("Duplicate definition for custom operator '" + op.Name + "' found.");
                    // Plain function?
                    if (op.Function != null)
                    {
                        if (op.Switch != null)
                            throw new ArgumentException("A CustomOperator cannot have both a Function and a Switch/Case block: " + op.Name);
                        context.Operators[name] = new Engine.Operators.CustomOperator(name)
                        {
                            ParamCount = op.ParamCount,
                            ParamNames = paramNameList.ToArray(),
                            ParamTypes = paramTypeList.ToArray(),
                            ReturnType = returnType,
                            Expression = ExpressionParser.ParseExpression(context.ReplaceParameters(op.Function), context)
                        };
                    }
                    else if (op.Switch != null)
                    {
                        if (null == op.Switch.Cases)
                            throw new ArgumentException("A CustomOperator with a Switch block must have at least one Case: " + op.Name);
                        int conditionCount = op.Switch.Cases.Length;
                        var conditions = new IExpression[conditionCount];
                        var caseFunctions = new IExpression[conditionCount];
                        for (int i = 0; i < conditionCount; ++i)
                        {
                            var thisCase = op.Switch.Cases[i];
                            conditions[i] = ExpressionParser.ParseExpression(context.ReplaceParameters(thisCase.Condition), context);
                            if (conditions[i].Operator.ReturnType != ParamType.Bool)
                                throw new ArgumentException("The return type of Case conditions must be boolean: " + thisCase.Condition + ", is " + conditions[i].Operator.ReturnType);
                            caseFunctions[i] = ExpressionParser.ParseExpression(context.ReplaceParameters(thisCase.Function), context);
                        }
                        var otherwise = ExpressionParser.ParseExpression(context.ReplaceParameters(op.Switch.Otherwise), context);

                        context.Operators[name] = new Engine.Operators.CustomOperator(name)
                        {
                            ParamCount = op.ParamCount,
                            ParamNames = paramNameList.ToArray(),
                            ParamTypes = paramTypeList.ToArray(),
                            ReturnType = returnType,
                            Conditions = conditions,
                            CaseFunctions = caseFunctions,
                            Otherwise = otherwise
                        };
                    }
                    else
                    {
                        throw new ArgumentException("A CustomOperator must either have a Function tag or a Switch/Case block: " + op.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An error occurred while parsing the custom operators: " + ex.Message);
            }
        }

        private void InitTransformer(ConfigFileXml configFile, Context context, TransformerFactory transformerFactory)
        {
            try
            {
                if (null == configFile.SourceTransform)
                    return;
                if (null == configFile.SourceTransform.Transform)
                    return;
                if (null == configFile.SourceTransform.Parameters)
                    configFile.SourceTransform.Parameters = [];

                var parameters = new IParameter[configFile.SourceTransform.Parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    var xmlParam = configFile.SourceTransform.Parameters[i];
                    if (null == xmlParam.FunctionString)
                        throw new ArgumentException("Parameter without function string found.");
                    if (null == xmlParam.Name)
                        throw new ArgumentException("Parameter without name found.");
                    parameters[i] = new TransformerParameter(
                        xmlParam.Name,
                        xmlParam.FunctionString,
                        ExpressionParser.ParseExpression(context.ReplaceParameters(xmlParam.FunctionString), context)
                    );
                }
                ISetting[]? settings = null;
                if (null != configFile.SourceTransform.Settings)
                {
                    settings = new ISetting[configFile.SourceTransform.Settings.Length];
                    for (int i = 0; i < configFile.SourceTransform.Settings.Length; ++i)
                    {
                        var xmlSetting = configFile.SourceTransform.Settings[i];
                        if (null == xmlSetting.Name)
                            throw new ArgumentException("Setting without name found.");
                        if (null == xmlSetting.Setting)
                            throw new ArgumentException("Setting without setting found.");
                        settings[i] = new TransformerSetting(xmlSetting.Name, xmlSetting.Setting);
                    }
                }
                else
                {
                    settings = new ISetting[0];
                }

                var transformer = transformerFactory.CreateTransformer(context,
                    configFile.SourceTransform.Transform.Uri,
                    configFile.SourceTransform.Transform.Config,
                    parameters,
                    settings);

                context.Transformer = transformer;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("An error occurred while instantiating the SourceTransform plugin: " + ex.Message);
            }
        }

        private static ConfigFileXml ReadConfigFile(string configFileName)
        {
            try
            {
                ConfigFileXml? configFile = null;
                bool errors = ValidateConfigFile(configFileName);

                if (errors)
                    throw new ArgumentException("XML config file could not be validated successfully.");

                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ConfigFileXml));
                using (var fs = new FileStream(configFileName, FileMode.Open))
                {
                    configFile = xmlSerializer.Deserialize(fs) as ConfigFileXml;
                }
                if (null == configFile)
                    throw new ArgumentException("Could not read XML config file: " + configFileName);
                return configFile;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Could not read XML config file: " + e.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private static bool ValidateConfigFile(string configFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "NoFrillsTransformation.Config.Config.xsd";

            bool errors = false;
            Stream? stream = null;
            try
            {
                stream = assembly.GetManifestResourceStream(resourceName);
                if (null == stream)
                    throw new InvalidOperationException("Could not find embedded resource: " + resourceName);
                using (var xsdStream = XmlReader.Create(stream))
                {
                    stream = null;
                    var schemaSet = new XmlSchemaSet();
                    schemaSet.Add("", xsdStream);
                    var xmlDoc = XDocument.Load(configFileName);
                    bool first = true;
                    xmlDoc.Validate(schemaSet, (o, e) =>
                    {
                        if (first)
                            Console.WriteLine("The input file does not adher to the input file schema:");
                        first = false;
                        Console.WriteLine(e.Message);
                        errors = true;
                    });
                }
            }
            finally
            {
                if (null != stream)
                    stream.Dispose();
                stream = null;
            }
            return errors;
        }

        private static ConfigFileXml[] ReadIncludes(IContext context, ConfigFileXml configFile)
        {
            if (null == configFile.Includes)
                configFile.Includes = [];
            int includeCount = configFile.Includes.Length;

            ConfigFileXml[] includes = new ConfigFileXml[includeCount];
            for (int i = 0; i < includeCount; ++i)
            {
                string resolvedPath = context.ResolveFileName(configFile.Includes[i].FileName);
                includes[i] = ReadConfigFile(resolvedPath);
            }
            return includes;
        }

        private static ConfigFileXml MergeConfigFiles(ConfigFileXml configFile, ConfigFileXml[] includes)
        {
            var configFiles = new List<ConfigFileXml>();
            configFiles.AddRange(includes);
            configFiles.Add(configFile);

            var customOperators = new List<CustomOperatorXml>();
            var lookupMaps = new List<LookupMapXml>();
            foreach (var cf in configFiles)
            {
                if (null != cf.CustomOperators)
                {
                    foreach (var customOperator in cf.CustomOperators)
                        customOperators.Add(customOperator);
                }
                if (null != cf.LookupMaps)
                {
                    foreach (var lookupMap in cf.LookupMaps)
                        lookupMaps.Add(lookupMap);
                }
            }

            configFile.CustomOperators = customOperators.ToArray();
            configFile.LookupMaps = lookupMaps.ToArray();
            return configFile;
        }

        private void Process(Context context)
        {
            try
            {
                context.Logger.Info("Started processing.");

                string[] outValues = new string[context.TargetFields.Length];
                string[] filterOutValues = [];
                if (context.FilterTargetFields != null)
                    filterOutValues = new string[context.FilterTargetFields.Length];

                var evaluator = new ExpressionParser();

                if (null == context.SourceReaders)
                {
                    context.SourceReaders = new ISourceReader[] { context.SourceReader };
                }

                for (int source = 0; source < context.SourceReaders.Length; ++source)
                {
                    context.Logger.Info("Reading Source #" + (source + 1));
                    try
                    {
                        context.SourceReader = context.SourceReaders[source];

                        while (!context.SourceReader.IsEndOfStream)
                        {
                            context.SourceReader.NextRecord();
                            if (context.SourceReader.IsEndOfStream)
                                break;
                            context.SourceRecordsRead++;

                            bool hasMultipleTransformedRecord = true;
                            bool isFirstRecord = true;

                            if (context.SourceRecordsRead % context.ProgressTick == 0
                                && context.SourceRecordsRead > 0)
                            {
                                context.Logger.Info("Processed " + context.SourceRecordsRead + " records.");
                            }

                            while (hasMultipleTransformedRecord)
                            {
                                if (null != context.Transformer)
                                {
                                    if (isFirstRecord)
                                    {
                                        try
                                        {
                                            isFirstRecord = false;
                                            context.InTransform = true;
                                            context.Transformer.Transform(context, evaluator);
                                        }
                                        finally
                                        {
                                            context.InTransform = false;
                                        }
                                    }
                                    else
                                    {
                                        context.Transformer.NextRecord();
                                    }
                                }
                                else
                                {
                                    hasMultipleTransformedRecord = false;
                                }

                                // Filter case?
                                if (context.CurrentRecordMatchesFilter(evaluator))
                                {
                                    // Normal case
                                    context.SourceRecordsProcessed++;

                                    for (int i = 0; i < outValues.Length; ++i)
                                    {
                                        outValues[i] = evaluator.Evaluate(evaluator, context.TargetFields[i].Expression, context);
                                    }

                                    context.TargetWriter.WriteRecord(outValues);
                                    context.TargetRecordsWritten++;
                                }
                                else
                                {
                                    // Filter case; do we write filtered fields?
                                    context.SourceRecordsFiltered++;

                                    if (null != context.FilterTargetFields
                                        && null != context.FilterTargetWriter)
                                    {
                                        for (int i = 0; i < filterOutValues.Length; ++i)
                                        {
                                            filterOutValues[i] = evaluator.Evaluate(evaluator, context.FilterTargetFields[i].Expression, context);
                                        }

                                        context.FilterTargetWriter.WriteRecord(filterOutValues);
                                    }
                                }

                                if (null != context.Transformer)
                                {
                                    hasMultipleTransformedRecord = context.Transformer.HasMoreRecords();
                                }
                            }
                        }
                    }
                    finally
                    {
                        // context.SourceReader = null;
                    }
                }

                if (context.Transformer != null)
                {
                    context.Logger.Info("Finishing Transform Process.");
                    context.Transformer.FinishTransform();
                }
                context.Logger.Info("Finishing Write Process.");
                context.TargetWriter.FinishWrite();

                context.Logger.Info("Finished processing.");
                context.Logger.Info("Total source records read: " + context.SourceRecordsRead);
                context.Logger.Info("Total target records written: " + context.TargetRecordsWritten);
                context.Logger.Info("Records filtered out: " + context.SourceRecordsFiltered);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("A processing error occurred in source row " + context.SourceRecordsRead + ": " + e.Message);
            }
        }

        private void InitLookupMaps(ConfigFileXml configFile, Context context, ReaderFactory readerFactory)
        {
            if (null == configFile.LookupMaps)
                return; // No lookup maps
            foreach (var lookupMap in configFile.LookupMaps)
            {
                context.LookupMaps.Add(lookupMap.Name, LookupMapFactory.CreateLookupMap(context, lookupMap, readerFactory));
                string nameLow = lookupMap.Name.ToLowerInvariant();
                if (context.Operators.ContainsKey(nameLow))
                    throw new InvalidOperationException("Duplicate use of operator '" + lookupMap.Name + "' for lookup maps; please rename the lookup map.");
                context.Operators[nameLow] = new NoFrillsTransformation.Engine.Operators.LookupOperator(nameLow);
            }

            context.Logger.Info("Initialized Lookup maps.");
        }

        private void ReadFilters(ConfigFileXml configFile, Context context)
        {
            try
            {
                context.FilterMode = FilterMode.And; // Default to and
                if (!string.IsNullOrWhiteSpace(configFile.FilterMode))
                {
                    string mode = configFile.FilterMode.ToLower();
                    if (mode.Equals("or"))
                        context.FilterMode = FilterMode.Or;
                }
                if (null == configFile.SourceFilters
                    || configFile.SourceFilters.Length == 0)
                    return; // No filters
                int filterCount = configFile.SourceFilters.Length;
                context.Filters = new FilterDef[filterCount];
                for (int i = 0; i < filterCount; ++i)
                {
                    var filterXml = configFile.SourceFilters[i];
                    context.Filters[i] = new FilterDef { Expression = ExpressionParser.ParseExpression(context.ReplaceParameters(filterXml.Expression), context) };
                    if (context.Filters[i].Expression.Operator.ReturnType != ParamType.Bool)
                        throw new InvalidOperationException("Source filter expression mismatch: Expression '" +
                            filterXml.Expression + "' does not evaluate to a boolean value.");
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("Could not read/parse filter definitions: " + e.Message);
            }

            context.Logger.Info("Initialized Filters.");
        }

        private void ReadMappings(ConfigFileXml configFile, Context context)
        {
            FieldsXml fields = GetFieldsXml(configFile);

            context.TargetFields = GetFieldDefs(context, fields);

            context.Logger.Info("Initialized field mappings, found " + context.TargetFields.Length + " output fields.");
        }

        private void ReadSourceMappings(ConfigFileXml configFile, Context context)
        {
            FieldsXml fields = GetFieldsXml(configFile);
            if (!fields.AppendSource)
                return;

            context.TargetFields = GetSourceFieldDefs(context, context.TargetFields);

            context.Logger.Info("Initialized field mappings, found " + context.TargetFields.Length + " output fields.");
        }

        private static FieldsXml GetFieldsXml(ConfigFileXml configFile)
        {
            if (null == configFile.Mappings && null == configFile.Fields)
                throw new ArgumentException("Configuration file misses Mappings or Fields section.");
            if (null != configFile.Mappings)
            {
                if (configFile.Mappings.Length == 0)
                    throw new ArgumentException("Configuration file does not have a valid Mapping (<Mapping> tag missing).");
                if (configFile.Mappings.Length > 1)
                    throw new ArgumentException("Multiple Mapping tags are not allowed currently.");
                var map = configFile.Mappings[0]; // Pick first mapping; it might be extended later on.
                if (null == map.Fields)
                    throw new ArgumentException("Missing field definitions in mapping.");

                return map.Fields;
            }
            // According to me, this check is superfluous, because if we reach this point, configFile.Fields is not null.
            // The compiler disagrees.
            if (null == configFile.Fields)
                throw new ArgumentException("Configuration file misses Fields section.");

            // Fields directly in Config File
            return configFile.Fields;
        }

        private void ReadFilterMappings(ConfigFileXml configFile, Context context)
        {
            FieldsXml? fields = null;
            if (null == configFile.FilterFields)
                return;
            fields = configFile.FilterFields;

            context.FilterTargetFields = GetFieldDefs(context, fields);

            context.Logger.Info("Initialized filter field mappings, found " + context.FilterTargetFields.Length + " output fields.");
        }

        private void ReadSourceFilterMappings(ConfigFileXml configFile, Context context)
        {
            FieldsXml? fields = null;
            if (null == configFile.FilterFields)
                return;
            fields = configFile.FilterFields;
            if (!fields.AppendSource)
                return;

            context.FilterTargetFields = GetSourceFieldDefs(context, context.FilterTargetFields);

            context.Logger.Info("Initialized filter field mappings, found " + context.FilterTargetFields.Length + " output fields.");
        }

        private static TargetFieldDef[] GetFieldDefs(Context context, FieldsXml fieldsXml)
        {
            int fieldCount = fieldsXml.Fields != null ? fieldsXml.Fields.Length : 0;
            var targetFields = new TargetFieldDef[fieldCount];

            FieldXml[] fields = fieldsXml.Fields ?? [];

            for (int i = 0; i < fieldCount; ++i)
            {
                var field = fields[i];
                if (null == field.Name)
                    throw new ArgumentException("Field definition misses name.");
                if (null == field.Expression)
                    throw new ArgumentException($"Field definition '${field.Name}' misses expression.");
                try
                {
                    var tfd = new TargetFieldDef(
                        context.ReplaceParameters(field.Name),
                        field.MaxSize,
                        (null != field.Config) ? context.ReplaceParameters(field.Config) : null,
                        ExpressionParser.ParseExpression(context.ReplaceParameters(field.Expression), context)
                    );
                    targetFields[i] = tfd;
                }
                catch (Exception e)
                {
                    throw new ArgumentException("An error occurred while parsing field '" + field.Name + "': " + e.Message);
                }
            }

            return targetFields;
        }

        private static TargetFieldDef[] GetSourceFieldDefs(Context context, TargetFieldDef[] previousMapping)
        {
            var reader = context.SourceReaders[0];

            var targetFields = new List<TargetFieldDef>();

            for (int i = 0; i < reader.FieldCount; ++i)
            {
                string fieldName = reader.FieldNames[i];
                var tfd = new TargetFieldDef(
                    fieldName,
                    0,
                    "",
                    ExpressionParser.ParseExpression(string.Format("${0}", fieldName), context)
                );

                targetFields.Add(tfd);
            }
            if (null != previousMapping)
                targetFields.AddRange(previousMapping);

            return targetFields.ToArray();
        }
    }
}
