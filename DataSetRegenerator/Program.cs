using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data.Design;
using System.IO;
using System.Text;

namespace MDWTypedDataSetGenerator
{

	class Program
	{

		const string usage = "\r\nUsage: DataSetGenerator -in <inputfile> [-out <outputfile>] [-namespace <namespace>]\r\n" +
			"   -in or -i: \\path\\to\\project\file.xsdr\n" +
			"   -our or -o: \\path\\to\\project\file.designer.cs - default is same as input but .designer.cs extension.\r\n" +
			"   -n or -namespace: namespace for generated file. Default is name of input file.\r\n" +
			"\r\nRuns the System.Data.Design.TypedDataSetGenerator on the specified file. Used when manual or build-step changes are made to the .xsd file to regenerate the .designer.cs file before building. Identical output to MSDataSetGenerator Visual Studio custom tool that only runs when you manually edit the .xsd.";

		static void Main()
		{
			// define and parse arguments. Default implementation can't handle paths or quoted arguments properly.
			ArgItemCollection argitems = new ArgItemCollection();

			argitems.Add(new ArgItem("in", "i", ArgTypeEnum.Path, true));
			argitems.Add(new ArgItem("out", "o", ArgTypeEnum.Path, false));
			argitems.Add(new ArgItem("namespace", "n", ArgTypeEnum.Text, false));
			try
			{
				MDWCLUtil.LoadArgs(argitems, Environment.CommandLine);
			}
			catch (Exception ex)
			{
				Console.WriteLine(MDWCLUtil.GetExceptionText(ex));
				Console.WriteLine(usage);
#if DEBUG
				Console.ReadKey();
#endif
				return;
			}

			if (!argitems.GetArgItem("out").Specified) argitems["out"] = Path.ChangeExtension(argitems["in"], ".designer.testcreate.cs");
			if (!argitems.GetArgItem("namespace").Specified) argitems["namespace"] = Path.GetFileNameWithoutExtension(argitems["in"]);

			StreamReader sr = null;
			string xsdFileContent = null;
			try
			{
				// read the input file and store in xsdFileContent
				sr = new StreamReader(argitems["in"]);
				xsdFileContent = sr.ReadToEnd();
				sr.Close();
			}
			finally
			{
				if (sr != null) sr.Close();
			}
			StreamWriter filewriter = null;
			try
			{
				var codeCompileUnit = new CodeCompileUnit();
				var codeNamespace = new CodeNamespace(argitems["namespace"]);
				Dictionary<string, string> providerOptions = new Dictionary<string, string>();
				providerOptions.Add("CompilerVersion", "v4");

				// HierarchicalUpdate = create a TableAdapterManager,  
				TypedDataSetGenerator.Generate(xsdFileContent, codeCompileUnit, codeNamespace, CodeDomProvider.CreateProvider("CSharp", providerOptions), TypedDataSetGenerator.GenerateOption.HierarchicalUpdate | TypedDataSetGenerator.GenerateOption.LinqOverTypedDatasets);

				filewriter = new StreamWriter(argitems["out"], false);

				var cscodeprovider = new CSharpCodeProvider();
				var generatorOptions = new CodeGeneratorOptions();

				// no idea why need both of these, you just do. Discovered this by dumb luck.
				cscodeprovider.GenerateCodeFromNamespace(codeNamespace, filewriter, generatorOptions);
				cscodeprovider.GenerateCodeFromCompileUnit(codeCompileUnit, filewriter, generatorOptions);
				filewriter.Close();
				filewriter = null;
			}
			catch (Exception ex)
			{
				Console.WriteLine(MDWCLUtil.GetExceptionText(ex));
				Console.WriteLine(usage);
#if DEBUG
				Console.ReadKey();
#endif
			}
			finally
			{
				if (filewriter != null) filewriter.Close();
			}

		}
	}


	public enum ArgTypeEnum { Text = 0, Number = 1, Path = 2, ParamOnly = 3 }

	public class ArgItem
	{
		public string ArgParam { get; protected set; }
		public string ArgParam2 { get; protected set; } // alternative item
		public ArgTypeEnum ArgType { get; protected set; }
		public bool Required { get; protected set; }
		public string Value { get; set; }
		public bool Specified { get; set; }
		/// <summary>
		/// Create Argument Item.
		/// </summary>
		/// <param name="argParam">Parameter name</param>
		/// <param name="argParam2">Alternate name (eg shorter)</param>
		/// <param name="argType">Type</param>
		/// <param name="required">True if required</param>
		public ArgItem(string argParam, string argParam2, ArgTypeEnum argType, bool required)
		{
			ArgParam = argParam.ToLower();
			ArgParam2 = argParam2?.ToLower();
			ArgType = argType;
			Value = null;
			Required = required;
		}
	}

	public class ArgItemCollection : System.Collections.ReadOnlyCollectionBase
	{
		public void Add(ArgItem item) { InnerList.Add(item); }
		public ArgItem this[int index]
		{
			get
			{
				if ((index < 0) || (index >= InnerList.Count)) throw new IndexOutOfRangeException();
				return (ArgItem)InnerList[index];
			}
		}
		public ArgItem GetArgItem(string param)
		{
			param = param.ToLower();
			for (int i = 0; i < InnerList.Count; i++) if (((ArgItem)InnerList[i]).ArgParam == param) return ((ArgItem)InnerList[i]);
			return null;
		}
		public string this[string param]
		{
			get
			{
				ArgItem item = GetArgItem(param);
				if (item == null) return null;
				else return item.Value;
			}
			set
			{
				ArgItem item = GetArgItem(param);
				if (item != null) item.Value = value;
			}
		}

	}


	public static class MDWCLUtil
	{
		/// <summary>
		/// Usage: MDWCLUtil.LoadArgs(argitems,Environment.CommandLine); Deals with " and paths correctly.
		/// </summary>
		/// <param name="argitems"></param>
		/// <param name="commandline"></param>
		public static void LoadArgs(ArgItemCollection argitems, string commandline)
		{
			// parse commandline
			// Rules: " ignored unless at start or end of an arg ie a space before and after
			// "stuff with spaces" => stuff with spaces
			// otherwise args are separated by a space
			// no escapes allowed \" cos this mucks up directories
			List<string> args = new List<string>();
			int i = 0;
			bool isquoted = false;
			int clen = commandline.Length;
			StringBuilder sb = new StringBuilder();
			while (i < clen)
			{
				char c = commandline[i++];
				if ((c == '"') && isquoted)
				{
					if ((i < clen) && (commandline[i] != ' ')) throw new Exception("Quote in middle of string not allowed: " + commandline);
					i++; // skip the ' '
					args.Add(sb.ToString());
					sb.Clear();
					isquoted = false;
				}
				else if ((c == '"') && (sb.Length == 0) && !isquoted)
				{
					isquoted = true;
				}
				else if ((c == ' ') && !isquoted)
				{
					if (sb.Length > 0) args.Add(sb.ToString());
					sb.Clear();
				}
				else sb.Append(c);
			}
			if (isquoted) throw new Exception("Unmatched quote: " + commandline);
			if (sb.Length > 0) args.Add(sb.ToString());

			string param = null;
			bool found;
			for (i = 1; i < args.Count; i++)
			{
				string arg = args[i].Trim();
				if (arg.StartsWith("-"))
				{

					if (arg.Length < 2) throw new Exception("Invalid parameter.");
					if (param != null) throw new Exception("Invalid parameter - argument value missing.");
					arg = arg.Substring(1).ToLower();
					found = false;
					foreach (ArgItem argitem in argitems)
					{
						if ((arg == argitem.ArgParam) || (!string.IsNullOrEmpty(argitem.ArgParam2) && (arg == argitem.ArgParam2)))
						{
							if (argitem.Specified) throw new Exception("Duplicate parameter: \"" + arg + "\"");
							argitem.Specified = true;
							if (!(argitem.ArgType == ArgTypeEnum.ParamOnly)) param = argitem.ArgParam;
							found = true;
							break;
						}
					}
					if (!found) throw new Exception("Unknown argument \"" + arg + "\"");
				}
				else
				{
					if (param == null) throw new Exception("Parameter expected, have paths/keys been quote-enclosed? \"" + arg + "\"");
					argitems[param] = arg;
					param = null;
				}
			}
			foreach (ArgItem argitem in argitems) if ((argitem.Required) && !argitem.Specified) throw new Exception("Required parameter missing: -" + argitem.ArgParam);
		}

		public static string GetExceptionText(Exception ex)
		{
			if (ex == null) return null;
			if (ex.InnerException != null) return ex.InnerException.Message;
			return ex.Message;
		}

	}












}
