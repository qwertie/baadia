﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Loyc.Collections;
using Loyc.Geometry;
using ProtoBuf;
using ProtoBuf.Meta;
using Util.WinForms;

namespace BoxDiagrams
{
	[ProtoContract()]
	public class DiagramDocumentCore
	{
		public DiagramDocumentCore()
		{
			Shapes = new MSet<Shape>();
			Styles = new DList<DiagramDrawStyle>();
		}

		[ProtoMember(1)]
		public DList<DiagramDrawStyle> Styles { get; set; }

		[ProtoMember(2, IsRequired=true)]
		public MSet<Shape> Shapes { get; set; }

		static byte[] FileSignature = new[] { (byte)'B', (byte)'&', (byte)'A', (byte)'s' };

		public void Save(Stream stream)
		{
			var model = GetProtobufModel();
			stream.Write(FileSignature, 0, FileSignature.Length);
			model.Serialize(stream, this);
		}
		public static DiagramDocumentCore Load(Stream stream)
		{
			var sig = new byte[FileSignature.Length];
			if (stream.Read(sig, 0, sig.Length) < sig.Length || !sig.Zip(FileSignature).All(p => p.A == p.B))
				throw new FormatException("Unrecognized file signature. This is not a Boxes & Arrows file.");
			var model = GetProtobufModel();
			var doc = (DiagramDocumentCore)model.Deserialize(stream, null, typeof(DiagramDocumentCore));
			doc.MarkPanels();
			return doc;
		}

		#region Protocol buffer configuration

		static RuntimeTypeModel _pbModel;
		static RuntimeTypeModel GetProtobufModel()
		{
			if (_pbModel == null) {
				_pbModel = TypeModel.Create();
				_pbModel.AllowParseableTypes=true;
				_pbModel.Add(typeof(Font), false).SetSurrogate(typeof(ProtoFont));
				_pbModel.Add(typeof(Color), false).SetSurrogate(typeof(ProtoColor));
				_pbModel.Add(typeof(StringFormat), false).SetSurrogate(typeof(ProtoStringFormat));
				_pbModel.Add(typeof(Point<float>), true).Add("X", "Y");
				_pbModel.Add(typeof(DrawStyle), true).AddSubType(100, typeof(DiagramDrawStyle));
				_pbModel[typeof(BoundingBox<float>)].Add("X1", "X2", "Y1", "Y2");
				_pbModel[typeof(BoundingBox<float>)].UseConstructor = false;
				Debug.WriteLine(_pbModel.GetSchema(typeof(DiagramDocumentCore)));
			}
			return _pbModel;
		}

		//[ProtoContract]
		//class ROList<T>
		//{
		//    public static implicit operator IReadOnlyList<T>(ROList<T> x) { return null; }
		//    public static implicit operator ROList<T>(IReadOnlyList<T> x) { return null; }
		//}

		[ProtoContract()]
		class ProtoFont
		{
			[ProtoMember(1)]
			string FontFamily;
			[ProtoMember(2)]
			float SizeInPoints;
			[ProtoMember(3)]
			FontStyle Style;
			
			public static implicit operator Font(ProtoFont f) {
				return new Font(f.FontFamily, f.SizeInPoints, f.Style);
			}
			public static implicit operator ProtoFont(Font f) { 
				return f == null ? null : new ProtoFont { FontFamily = f.FontFamily.Name, SizeInPoints = f.SizeInPoints, Style = f.Style };
			}
		}
		[ProtoContract()]
		class ProtoStringFormat
		{
			[ProtoMember(1, DataFormat=DataFormat.Group)]
			StringAlignment Alignment;
			[ProtoMember(2)]
			StringAlignment LineAlignment;
			[ProtoMember(3)]
			StringFormatFlags Flags;
			public static implicit operator StringFormat(ProtoStringFormat f) { 
				return new StringFormat(f.Flags) { Alignment = f.Alignment, LineAlignment = f.LineAlignment };
			}
			public static implicit operator ProtoStringFormat(StringFormat f) { 
				return f == null ? null : new ProtoStringFormat() { Flags = f.FormatFlags, Alignment = f.Alignment, LineAlignment = f.LineAlignment };
			}
		}
		[ProtoContract]
		struct ProtoColor
		{
			[ProtoMember(1, DataFormat=DataFormat.FixedSize)]
			public uint argb;
			public static implicit operator Color(ProtoColor c) { return Color.FromArgb((int)c.argb); }
			public static implicit operator ProtoColor(Color c) { return new ProtoColor { argb = (uint)c.ToArgb() }; }
		}

		#endregion

		public void MarkPanels()
		{
			// "Panels" are defined as text shapes that have other shapes 
			// entirely inside them. The UI treats them a little differently
			// though they look the same, e.g. you can draw a new box on top of one.
			var shapes = Shapes.ToList();
			shapes.Sort((a, b) => a.BBox.Area().CompareTo(b.BBox.Area()));
			for (int bigger = shapes.Count - 1; bigger >= 0; bigger--) {
				var biggerS = shapes[bigger] as TextBox;
				if (biggerS != null) {
					var biggerBox = biggerS.BBox;
					biggerS._isPanel = false;
					for (int i = 0; i < bigger; i++) {
						if (biggerBox.Contains(shapes[i].BBox)) {
							biggerS._isPanel = true;
							break;
						}
					}
				}
			}
		}
	}

	public class DiagramDrawStyle : DrawStyle
	{
		public string Name;

		//public Color LineColor { get { return base.LineColor; } }
		//public float LineWidth { get { return base.LineWidth; } }
		//public DashStyle LineStyle { get { return base.LineStyle; } }
		//public Color FillColor { get { return base.FillColor; } }
		//public Color TextColor { get { return base.TextColor; } }
		//public Font Font { get { return base.Font; } }

		public override DrawStyle Clone()
		{
			var copy = base.Clone();
			Debug.Assert(((DiagramDrawStyle)copy).Name == Name);
			return copy;
		}
	}
}

