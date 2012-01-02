﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using N2.Engine;
using N2.Web;

namespace N2.Persistence.Sources
{
	public abstract class SourceBase<T> : SourceBase
	{
		public SourceBase()
		{
			BaseContentType = typeof(T);
		}

		public override bool IsProvidedBy(ContentItem item)
		{
			return item is T;
		}
	}

	public abstract class SourceBase : IComparable<SourceBase>
	{
		private IEngine engine;

		public SourceBase()
		{
			BaseContentType = typeof(ContentItem);
		}

		public virtual IEngine Engine
		{
			get { return engine ?? (engine = N2.Context.Current); }
			set { engine = value; }
		}

		public Type BaseContentType { get; set; }
		public virtual int SortOrder 
		{ 
			get 
			{ 
				return 200
					- (BaseContentType.IsInterface ? 100 : 0)
					- 10 * Utility.InheritanceDepth(GetType()) 
					- Utility.InheritanceDepth(BaseContentType); 
			}
		}

		public virtual bool ProvidesChildrenFor(ContentItem parent)
		{
			return true;
		}

		public abstract IEnumerable<ContentItem> AppendChildren(IEnumerable<ContentItem> previousChildren, Query query);

		public abstract bool IsProvidedBy(ContentItem item);

		public virtual PathData ResolvePath(string path)
		{
			return PathData.Empty;
		}

		public virtual PathData ResolvePath(ContentItem startingPoint, string path)
		{
			return PathData.Empty;
		}

		#region IComparable<ContentSource> Members

		public virtual int CompareTo(SourceBase other)
		{
			return this.SortOrder - other.SortOrder;
		}

		#endregion

		public abstract ContentItem Get(object id);
		public abstract void Save(ContentItem item);
		public abstract void Delete(ContentItem item);
		public abstract ContentItem Move(ContentItem source, ContentItem destination);
		public abstract ContentItem Copy(ContentItem source, ContentItem destination);

		protected IEnumerable<ContentItem> AppendContentChildren(IEnumerable<ContentItem> previousChildren, Query query)
		{
			IEnumerable<ContentItem> items;
			if (!query.OnlyPages.HasValue)
				items = query.Parent.Children;
			else if (query.OnlyPages.Value)
				items = query.Parent.Children.FindPages();
			else
				items = query.Parent.Children.FindParts();

			if (query.Filter != null)
				items = items.Where(query.Filter);

			return previousChildren.Union(items);
		}
	}
}
