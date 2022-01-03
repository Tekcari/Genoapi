using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tekcari.Gapi.Serialization;

namespace Tekcari.Gapi.Generators.Csharp
{
	public class CsharpClientGenerator : IGenerator<CsharpClientGeneratorSettings>
	{
		public CsharpClientGenerator()
			: this(new CsharpClientGeneratorSettings(), new CsharpTypeAdapter()) { }

		public CsharpClientGenerator(CsharpClientGeneratorSettings settings)
			: this(settings, new CsharpTypeAdapter()) { }

		public CsharpClientGenerator(CsharpClientGeneratorSettings settings, ITypeNameAdapter<CsharpClientGeneratorSettings> typeNameAdapter)
		{
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
			_mapper = typeNameAdapter ?? throw new ArgumentNullException(nameof(typeNameAdapter));

			DotLiquid.Template.RegisterFilter(typeof(CsharpFilters));
			DotLiquid.Template.RegisterFilter(typeof(CustomFilters));
		}

		public FileResult[] Generate(OpenApiDocument document) => Generate(document, _settings);

		public FileResult[] Generate(OpenApiDocument document, CsharpClientGeneratorSettings settings)
		{
			_document = document ?? throw new ArgumentNullException(nameof(document));
			_settings = settings;

			BuildGlobalModel();
			GenerateSupportClasses();
			GenerateComponents();
			GenerateClientClass();

			return _fileList.ToArray();
		}

		// ==================== INTERNAL MEMBERS ==================== //

		private static string ExpandEndpointPath(OpenApiOperation operation, string path)
		{
			var url = new StringBuilder(path);

			if (operation.Parameters.Any(x => x.In == ParameterLocation.Query))
				url.Append('?');

			var queryValues = from x in operation.Parameters
							  where x.In == ParameterLocation.Query && !IsArrary(x.Schema)
							  select $"{x.Name}={{{x.Name}}}";
			if (queryValues.Any())
				url.Append(string.Join("&", queryValues));

			queryValues = from x in operation.Parameters
						  where x.In == ParameterLocation.Query && IsArrary(x.Schema)
						  select $"{{GetQueryList(\"{x.Name}\", {x.Name})}}";
			if (queryValues.Any())
				url.Append(string.Join("&", queryValues));

			return url.ToString();
		}

		private void BuildGlobalModel()
		{
			_globalModel.Add("base_url", _settings.BaseUrl);
			_globalModel.Add("references", _settings.References);
			_globalModel.Add("rootnamespace", _settings.RootNamespace);
			_globalModel.Add("client_class_name", _settings.ClientClassName);
		}

		private void GenerateSupportClasses()
		{
			var template = CreateTemplate(EmbeddedResourceName.Response);
			string content = template.Render(_globalModel);

			_fileList.Add(new FileResult("Response.cs", content, "native"));
		}

		private void GenerateComponents()
		{
			foreach (KeyValuePair<string, OpenApiSchema> schema in _document.Components.Schemas)
				if (_settings.ClassesToExludeFromComponents?.Contains(schema.Key) ?? false) continue;
				else if (string.Equals(schema.Value.Type, "object", StringComparison.InvariantCultureIgnoreCase))
				{
					DotLiquid.Hash model = DotLiquid.Hash.FromAnonymousObject(GetClassProperties(schema.Key, schema.Value));
					model.Merge(_globalModel);

					DotLiquid.Template template = CreateTemplate(EmbeddedResourceName.components);
					_fileList.Add(new FileResult($"{CsharpFilters.SafeName(schema.Key)}.cs", template.Render(model)?.Trim(), "component"));
				}
				else if (schema.Value.Enum.Any())
				{
					DotLiquid.Hash model = DotLiquid.Hash.FromAnonymousObject(GetEnumValues(schema.Key, schema.Value));
					model.Merge(_globalModel);

					DotLiquid.Template template = CreateTemplate(EmbeddedResourceName.enumeration);
					_fileList.Add(new FileResult($"{CsharpFilters.SafeName(schema.Key)}.cs", template.Render(model)?.Trim(), "enum"));
				}
		}

		private void GenerateClientClass()
		{
			DotLiquid.Template template = CreateTemplate(EmbeddedResourceName.httpClient);
			var endpoints = new List<object>();
			var model = new DotLiquid.Hash();

			foreach (KeyValuePair<string, OpenApiPathItem> path in _document.Paths)
				foreach (KeyValuePair<OperationType, OpenApiOperation> operation in path.Value.Operations)
				{
					object endpoint = DotLiquid.Hash.FromAnonymousObject(GetEndpointModel(path.Key, operation.Key, operation.Value));
					endpoints.Add(endpoint);
				}

			model.Add("endpoints", endpoints);
			model.Merge(_globalModel);

			string content = template.Render(model);
			_fileList.Add(new FileResult($"{_settings.ClientClassName}.cs", content, "client"));
		}

		private object GetClassProperties(string className, OpenApiSchema schema)
		{
			var properties = new List<object>();
			foreach (KeyValuePair<string, OpenApiSchema> member in schema.Properties)
			{
				properties.Add(GetEntityProperty(member.Key, member.Value));
			}

			return new { className, properties };
		}

		private object GetEnumValues(string className, OpenApiSchema schema)
		{
			var properties = new List<object>();
			foreach (IOpenApiAny member in schema.Enum)
				if (member is OpenApiInteger integer)
				{
					bool numeric = (schema?.Type ?? string.Empty).StartsWith("int", StringComparison.InvariantCultureIgnoreCase);
					string value = Convert.ToString(integer.Value);
					string name = numeric ? $"Value{value}" : value;

					properties.Add(new { name, value });
				}

			return new { className, properties };
		}

		private object GetEntityProperty(string propertyName, OpenApiSchema schema)
		{
			return new
			{
				name = propertyName,
				type = _mapper.Map(schema, _settings),
				summary = NullIfWhitespace(schema.Description),
				example = schema.Example
			};
		}

		private object GetEndpointModel(string path, OperationType method, OpenApiOperation operation)
		{
			List<object> parameters = GetEndpointParameterList(operation);
			string returnType = GetEndpointReturnType(operation);
			string url = ExpandEndpointPath(operation, path);

			return new
			{
				path = url,
				method = Enum.GetName(typeof(OperationType), method),
				operationName = operation.OperationId,
				summary = NullIfWhitespace(operation.Summary),
				remarks = NullIfWhitespace(operation.Description),
				returnType,
				parameters
			};
		}

		private List<object> GetEndpointParameterList(OpenApiOperation operation)
		{
			static string safeName(string x) => CustomFilters.CamelCase(CsharpFilters.SafeName(x));
			var parameters = new List<object>();

			if (operation.RequestBody != null)
			{
				KeyValuePair<string, OpenApiMediaType> first = operation.RequestBody.Content.FirstOrDefault();
				string mimeType = first.Key.ToLowerInvariant();
				OpenApiMediaType mediaType = first.Value;
				string name, kind = "body", type = _mapper.Map(mediaType.Schema, _settings);

				switch (mimeType)
				{
					case "application/xml":
					case "application/json":
					case "application/x-www-form-urlencoded":
						name = safeName(type);
						parameters.Add(new { kind, type, name, value = $"{type} {name}", mimeType });
						break;

					case "application/octet-stream":
						name = operation.Parameters.Any(x => x.Name == "data") ? "data1" : "data";
						parameters.Add(new { kind, type, name, value = $"{type} {name}", mimeType });
						break;
				}
			}

			string lastItem = operation.Parameters?.LastOrDefault()?.Name;
			foreach (OpenApiParameter arg in operation.Parameters.OrderByDescending(x => x.Required))
			{
				parameters.Add(GetEndpointParameter(arg, arg.Name == lastItem));
			}

			return parameters;
		}

		private object GetEndpointParameter(OpenApiParameter parameter, bool islastItem)
		{
			static bool isArrary(OpenApiSchema x) => string.Equals(x.Type, "array", StringComparison.InvariantCultureIgnoreCase);

			string name = parameter.Name;
			string type = _mapper.Map(parameter.Schema, _settings);
			string value = $"{type} {name}";
			string kind = Enum.GetName(typeof(ParameterLocation), parameter.In);

			if (islastItem && isArrary(parameter.Schema) && parameter.Required == false) value = string.Concat("params ", value);
			else if (parameter.Required == false) value = string.Concat(value, " = default");

			return new { name, type, value, kind, description = NullIfWhitespace(parameter.Description) };
		}

		private string GetEndpointReturnType(OpenApiOperation operation)
		{
			foreach (KeyValuePair<string, OpenApiResponse> response in operation.Responses)
				if (int.TryParse(response.Key, out int code))
				{
					if (code >= 200 && code <= 299)
					{
						OpenApiMediaType mediaType = response.Value.Content.FirstOrDefault().Value;
						if (mediaType == null) return string.Empty;
						else return _mapper.Map(mediaType.Schema, _settings);
					}
				}

			return null;
		}

		#region Backing Members

		private readonly DotLiquid.Hash _globalModel = new DotLiquid.Hash();
		private readonly ITypeNameAdapter<CsharpClientGeneratorSettings> _mapper = new CsharpTypeAdapter();

		private OpenApiDocument _document;
		private CsharpClientGeneratorSettings _settings;
		private ICollection<FileResult> _fileList = new List<FileResult>();

		private static bool IsArrary(OpenApiSchema x) => string.Equals(x.Type, "array", StringComparison.InvariantCultureIgnoreCase);

		private static string NullIfWhitespace(string x) => string.IsNullOrWhiteSpace(x) ? null : x;

		private DotLiquid.Template CreateTemplate(string fileName) => DotLiquid.Template.Parse(ResourceLoader.GetContent(fileName));

		#endregion Backing Members
	}
}