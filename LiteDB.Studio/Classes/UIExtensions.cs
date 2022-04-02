using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Data;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

#if TREE_DATA_GRID
using Avalonia.Controls.Models.TreeDataGrid;
using AppDataGrid = Avalonia.Controls.TreeDataGrid;
#else
using AppDataGrid = Avalonia.Controls.DataGrid;
#endif


namespace LiteDB.Studio
{
	static class UiExtensions
	{
		public static void BindBsonData(this AppDataGrid grd, TaskData data)
		{
			// hide grid if has more than 100 rows
			grd.IsVisible = data.Result.Count < 100;
			grd.Clear();

#if TREE_DATA_GRID
			var source = new FlatTreeDataGridSource<BsonValue>(data.Result);
			var checkList = new Dictionary<string, IColumn<BsonValue>>();

			foreach (var value in data.Result)
			{
				//var row = new DataGridRow();

				var doc = value.IsDocument ? value.AsDocument : new BsonDocument { ["[value]"] = value };

				if (doc.Keys.Count == 0) doc["[root]"] = "{}";

				foreach (var key in doc.Keys)
				{
					if (checkList.ContainsKey(key)) continue;

					IColumn<BsonValue> col = doc[key].Type switch
					{
						BsonType.Boolean => new TextColumn<BsonValue, bool>(
							key,
							v => v[key].AsBoolean,
							(b, v) => b[key] = v),
						_ => new TextColumn<BsonValue, string>(
							key,
							v => v[key].ToString(),
							(b, v) => b[key] = v),
					};

					checkList.Add(key, col);
					source.Columns.Add(col);
				}
			}
			grd.Source = source;
#else
			foreach (var value in data.Result)
			{
				var doc = value.IsDocument ? value.AsDocument : new BsonDocument { ["value"] = value };

				if (doc.Keys.Count == 0) doc["[root]"] = "{}";

				foreach (var key in doc.Keys)
				{
					if (grd.Columns.Any(f => f.Header?.ToString() == key)) continue;

					DataGridBoundColumn col = doc[key].Type switch
					{
						BsonType.Boolean => new DataGridCheckBoxColumn() { Header = key },
						_ => new DataGridTextColumn() { Header = key }
					};

					grd.Columns.Add(col);

					col.Width = DataGridLength.Auto;
					col.IsReadOnly = key == "_id" ||
					                 doc[key].Type is BsonType.Document
						                 or BsonType.Array
						                 or BsonType.ObjectId
						                 or BsonType.Binary;

					col.Binding = new Binding
					{
						Mode = BindingMode.TwoWay,
						Path = key
					};
				}
			}

			grd.IsReadOnly = grd.Columns.All(f => f.Header?.ToString() != "_id");
			grd.Items = data.Result;
#endif

			grd.IsVisible = true;
		}

		public static void Clear(this AppDataGrid grd)
		{
#if TREE_DATA_GRID
			var pass = grd.Source;
			grd.Source = null;
			(pass as IDisposable)?.Dispose();
#else
			grd.Columns.Clear();
			grd.Items = null;
#endif
		}

		public static void BindBsonData(
			this TextEditor txt,
			TextMate.Installation textMate,
			RegistryOptions registryOptions,
			TaskData data)
		{
			var index = 0;
			var sb = new StringBuilder();

			using (var writer = new StringWriter(sb))
			{
				var json = new JsonWriter(writer)
				{
					Pretty = true,
					Indent = 2
				};

				if (data.Result.Count > 0)
				{
					foreach (var value in data.Result)
					{
						if (data.Result?.Count > 1)
						{
							sb.AppendLine($"/* {index++ + 1} */");
						}

						json.Serialize(value);
						sb.AppendLine();
					}

					if (data.LimitExceeded)
					{
						sb.AppendLine();
						sb.AppendLine("/* Limit exceeded */");
					}
				}
				else
				{
					sb.AppendLine("no result");
				}
			}

			textMate.SetGrammar(
				registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".json").Id));
			txt.Text = sb.ToString();
		}

		public static void BindErrorMessage(this AppDataGrid grid, string sql, Exception ex)
		{
			grid.Clear();
#if TREE_DATA_GRID
#else
			var col = new DataGridTextColumn() { Header = "Error" };
			grid.Columns.Add(col);

			col.Width = DataGridLength.Auto;

			col.IsReadOnly = true;
			col.Binding = new Binding();

			grid.Items = new List<string>() { ex.Message };
#endif
		}

		public static void BindErrorMessage(
			this TextEditor txt,
			TextMate.Installation textMate,
			RegistryOptions registryOptions,
			string sql,
			Exception ex)
		{
			var sb = new StringBuilder();

			if (!(ex is LiteException))
			{
				sb.AppendLine(ex.Message);
				sb.AppendLine();
				sb.AppendLine("===================================================");
				sb.AppendLine(ex.StackTrace);
			}
			else if (ex is LiteException lex)
			{
				sb.AppendLine(ex.Message);

				if (lex.ErrorCode == LiteException.UNEXPECTED_TOKEN && sql != null)
				{
					var p = (int)lex.Position;
					var start = (int)Math.Max(p - 30, 1) - 1;
					var end = Math.Min(p + 15, sql.Length);
					var length = end - start;

					var str = sql.Substring(start, length).Replace('\n', ' ').Replace('\r', ' ');
					var t = length - (end - p);

					sb.AppendLine();
					sb.AppendLine(str);
					sb.AppendLine("".PadLeft(t, '-') + "^");
				}
			}

			textMate.SetGrammar(null);
			txt.Clear();
			txt.Text = sb.ToString();
		}

		public static void BindParameter(
			this TextEditor txt,
			TextMate.Installation textMate,
			RegistryOptions registryOptions,
			TaskData data)
		{
			txt.Clear();
			textMate.SetGrammar(
				registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".json").Id));

			var sb = new StringBuilder();

			using (var writer = new StringWriter(sb))
			{
				var w = new JsonWriter(writer)
				{
					Pretty = true,
					Indent = 2
				};

				w.Serialize(data.Parameters ?? BsonValue.Null);
			}

			txt.Text = sb.ToString();
		}
	}
}
