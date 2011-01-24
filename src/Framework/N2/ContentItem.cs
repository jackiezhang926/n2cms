#region License
/* Copyright (C) 2006-2009 Cristian Libardo
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Web;
using N2.Collections;
using N2.Details;
using N2.Integrity;
using N2.Persistence;
using N2.Web;
using N2.Edit.Workflow;
using N2.Definitions;
using N2.Persistence.Proxying;

namespace N2
{
    /// <summary>
    /// The base of N2 content items. All content pages and data items are 
    /// derived from this item. During the initialization phase the CMS looks 
    /// for classes deriving from <see cref="ContentItem"/> marked with the 
    /// <see cref="DefinitionAttribute"/> and makes them available for
    /// editing and storage in the database.
    /// </summary>
    /// <example>
    /// // Since the class is inheriting <see cref="ContentItem"/> it's 
    /// // recognized by the CMS and made available for editing.
    /// [PageDefinition(TemplateUrl = "~/Path/To/My/Template.aspx")]
    /// public class MyPage : N2.ContentItem
    /// {
    ///	}
    /// </example>
    /// <remarks>
    /// Note that the class name (e.g. MyPage) is used as discriminator when
    /// retrieving items from database storage. If you change the class name 
    /// you should manually change the discriminator in the database or set the 
    /// name of the definition attribute, e.g. [Definition("Title", "OldClassName")]
    /// </remarks>
	[Serializable, DebuggerDisplay("{GetType().Name}: {Name}#{ID}")]
	[DynamicTemplate]
	[RestrictParents(typeof(ContentItem))]
	[SortChildren(SortBy.CurrentOrder)]
	public abstract class ContentItem : IComparable, 
		IComparable<ContentItem>, 
		ICloneable,
		IDependentEntity<IUrlParser>, 
		INode, 
		IUpdatable<ContentItem>, 
		IInterceptableType
    {
        #region Private Fields
        private int id;
        private string title;
        private string name;
        private string zoneName;
		private ContentItem parent = null;
        private DateTime created;
        private DateTime updated;
        private DateTime? published = DateTime.Now;
        private DateTime? expires = null;
        private int sortOrder;
		private string url = null;
        private bool visible = true;
		private ContentItem versionOf = null;
		private string savedBy;
		private IList<Security.AuthorizedRole> authorizedRoles = null;
		private IList<ContentItem> children = new List<ContentItem>();
		private IDictionary<string, Details.ContentDetail> details = new Dictionary<string, Details.ContentDetail>();
		private IDictionary<string, Details.DetailCollection> detailCollections = new Dictionary<string, Details.DetailCollection>();
		[NonSerialized]
		private Web.IUrlParser urlParser;
    	private string ancestralTrail;
        private int versionIndex;
        private ContentState state = ContentState.None;
		private N2.Security.Permission alteredPermissions = N2.Security.Permission.None;
        #endregion

        #region Constructor
        /// <summary>Creates a new instance of the ContentItem.</summary>
		public ContentItem()
        {
            created = DateTime.Now;
            updated = DateTime.Now;
            published = DateTime.Now;
        }
        #endregion

		#region Persisted Properties
		/// <summary>Gets or sets item ID.</summary>
		public virtual int ID
		{
			get { return id; }
			set { id = value; }
		}

		/// <summary>Gets or sets this item's parent. This can be null for root items and previous versions but should be another page in other situations.</summary>
		public virtual ContentItem Parent
		{
			get { return parent; }
			set { parent = value; }
		}

		/// <summary>Gets or sets the item's title. This is used in edit mode and probably in a custom implementation.</summary>
		[Details.Displayable(typeof(Web.UI.WebControls.Hn), "Text")]
		public virtual string Title
		{
			get { return title; }
			set { title = value; }
		}

        private static char[] invalidCharacters = new char[] { '%', '?', '&', '/', ':' };
		/// <summary>Gets or sets the item's name. This is used to compute the item's url and can be used to uniquely identify the item among other items on the same level.</summary>
		public virtual string Name
		{
			get 
            { 
                return name ?? (ID > 0 ? ID.ToString() : null); 
            }
			set 
            {
                //if (value != null && value.IndexOfAny(invalidCharacters) >= 0) throw new N2Exception("Invalid characters in name, '%', '?', '&', '/', ':', '+', '.' not allowed.");
                if (string.IsNullOrEmpty(value))
                    name = null;
                else
                    name = value; 
                url = null;  
            }
		}

		/// <summary>Gets or sets zone name which is associated with data items and their placement on a page.</summary>
        [DisplayableLiteral]
        public virtual string ZoneName
		{
			get { return zoneName; }
			set { zoneName = value; }
		}

		/// <summary>Gets or sets when this item was initially created.</summary>
        [DisplayableLiteral]
        public virtual DateTime Created
		{
			get { return created; }
			set { created = value; }
		}

		/// <summary>Gets or sets the date this item was updated.</summary>
        [DisplayableLiteral]
        public virtual DateTime Updated
		{
			get { return updated; }
			set { updated = value; }
		}

		/// <summary>Gets or sets the publish date of this item.</summary>
		[DisplayableLiteral]
        public virtual DateTime? Published
		{
			get { return published; }
			set { published = value; }
		}

		/// <summary>Gets or sets the expiration date of this item.</summary>
        [DisplayableLiteral]
        public virtual DateTime? Expires
		{
			get { return expires; }
			set { expires = value != DateTime.MinValue ? value : null; }
		}

		/// <summary>Gets or sets the sort order of this item.</summary>
        [DisplayableLiteral]
        public virtual int SortOrder
		{
			get { return sortOrder; }
			set { sortOrder = value; }
		}

		/// <summary>Gets or sets whether this item is visible. This is normally used to control its visibility in the site map provider.</summary>
        [DisplayableLiteral]
        public virtual bool Visible
		{
			get { return visible; }
			set { visible = value; }
		}

		/// <summary>Gets or sets the published version of this item. If this value is not null then this item is a previous version of the item specified by VersionOf.</summary>
		public virtual ContentItem VersionOf
		{
			get { return versionOf; }
			set { versionOf = value; }
		}

		/// <summary>Gets or sets the name of the identity who saved this item.</summary>
        [DisplayableLiteral]
        public virtual string SavedBy
		{
			get { return savedBy; }
			set { savedBy = value; }
		}

		/// <summary>Gets or sets the details collection. These are usually accessed using the e.g. item["Detailname"]. This is a place to store content data.</summary>
		public virtual IDictionary<string, Details.ContentDetail> Details
		{
			get { return details; }
			set { details = value; }
		}

		/// <summary>Gets or sets the details collection collection. These are details grouped into a collection.</summary>
		public virtual IDictionary<string, Details.DetailCollection> DetailCollections
		{
			get { return detailCollections; }
			set { detailCollections = value; }
		}

		/// <summary>Gets or sets all a collection of child items of this item ignoring permissions. If you want the children the current user has permission to use <see cref="GetChildren()"/> instead.</summary>
		public virtual IList<ContentItem> Children
		{
			get { return children; }
			set { children = value; }
		}

		/// <summary>Represents the trail of id's uptil the current item e.g. "/1/10/14/"</summary>
		public virtual string AncestralTrail
		{
			get { return ancestralTrail; }
			set { ancestralTrail = value; }
        }

        /// <summary>The version number of this item</summary>
        [DisplayableLiteral]
        public virtual int VersionIndex
        {
            get { return versionIndex; }
            set { versionIndex = value; }
        }

        [DisplayableLiteral]
        public virtual ContentState State
        {
            get { return state; }
            set { state = value; }
        }

        [DisplayableLiteral]
		public virtual N2.Security.Permission AlteredPermissions
        {
			get { return alteredPermissions; }
			set { alteredPermissions = value; }
        }
		#endregion

		#region Generated Properties
		/// <summary>The default file extension for this content item, e.g. ".aspx".</summary>
        public virtual string Extension
        {
            get { return Web.Url.DefaultExtension; }
        }

		/// <summary>Gets whether this item is a page. This is used for and site map purposes.</summary>
		public virtual bool IsPage
		{
			get { return Definitions.Static.DescriptionDictionary.GetDescription(GetContentType()).IsPage; }
		}

		/// <summary>Gets the public url to this item. This is computed by walking the parent path and prepending their names to the url.</summary>
		public virtual string Url
		{
			get 
			{
				if(url == null)
				{
					if (urlParser != null)
						url = urlParser.BuildUrl(this);
					else
						url = FindPath(PathData.DefaultAction).RewrittenUrl;
				}
				return url;
			}
		}

		/// <summary>Gets the template that handle the presentation of this content item. For non page items (IsPage) this can be a user control (ascx).</summary>
        public virtual string TemplateUrl
        {
            get { return "~/Default.aspx"; }
        }
		
		/// <summary>Gets the icon of this item. This can be used to distinguish item types in edit mode.</summary>
		public virtual string IconUrl
        {
			get { return N2.Web.Url.ResolveTokens(Definitions.Static.DescriptionDictionary.GetDescription(GetContentType()).IconUrl); }
        }

		/// <summary>Gets the non-friendly url to this item (e.g. "/Default.aspx?page=1"). This is used to uniquely identify this item when rewriting to the template page. Non-page items have two query string properties; page and item (e.g. "/Default.aspx?page=1&amp;item&#61;27").</summary>
		[Obsolete("Use the new template API: item.FindPath(PathData.DefaultAction).RewrittenUrl")]
		public virtual string RewrittenUrl
		{
			get { return FindPath(PathData.DefaultAction).RewrittenUrl; }
		}

		#endregion

		#region Security
		/// <summary>Gets an array of roles allowed to read this item. Null or empty list is interpreted as this item has no access restrictions (anyone may read).</summary>
		public virtual IList<Security.AuthorizedRole> AuthorizedRoles
		{
			get 
			{
				if (authorizedRoles == null)
					authorizedRoles = new List<Security.AuthorizedRole>();
				return authorizedRoles;
			}
			set { authorizedRoles = value; }
		}

		#endregion

        #region this[]

		/// <summary>Gets or sets the detail or property with the supplied name. If a property with the supplied name exists this is always returned in favour of any detail that might have the same name.</summary>
		/// <param name="detailName">The name of the propery or detail.</param>
		/// <returns>The value of the property or detail. If now property exists null is returned.</returns>
		public virtual object this[string detailName]
        {
            get
            {
				if (detailName == null)
					throw new ArgumentNullException("detailName");

                switch (detailName)
                {
                    case "ID":
                        return ID;
                    case "Title":
                        return Title;
                    case "Name":
                        return Name;
                    case "Url":
                        return Url;
                    case "TemplateUrl":
                        return TemplateUrl;
                    default:
						return Utility.Evaluate(this, detailName)
							?? GetDetail(detailName)
							?? GetDetailCollection(detailName, false);
                }
            }
            set 
            {
                if (string.IsNullOrEmpty(detailName))
					throw new ArgumentNullException("Parameter 'detailName' cannot be null or empty.", "detailName");

                PropertyInfo info = GetContentType().GetProperty(detailName);
				if (info != null && info.CanWrite)
				{
					if (value != null && info.PropertyType != value.GetType())
						value = Utility.Convert(value, info.PropertyType);
					info.SetValue(this, value, null);
				}
				else if (value is Details.DetailCollection)
					throw new N2Exception("Cannot set a detail collection this way, add it to the DetailCollections collection instead.");
				else
				{
					SetDetail(detailName, value);
				}       
            }
        }
        #endregion

		#region GetDetail & SetDetail<T> Methods
		/// <summary>Gets a detail from the details bag.</summary>
		/// <param name="detailName">The name of the value to get.</param>
		/// <returns>The value stored in the details bag or null if no item was found.</returns>
		public virtual object GetDetail(string detailName)
		{
			return Details.ContainsKey(detailName)
				? Details[detailName].Value
				: null;
		}

        /// <summary>Gets a detail from the details bag.</summary>
        /// <param name="detailName">The name of the value to get.</param>
        /// <param name="defaultValue">The default value to return when no detail is found.</param>
        /// <returns>The value stored in the details bag or null if no item was found.</returns>
        public virtual T GetDetail<T>(string detailName, T defaultValue)
        {
            return Details.ContainsKey(detailName)
                ? (T)Details[detailName].Value
                : defaultValue;
        }

		/// <summary>Set a value into the <see cref="Details"/> bag. If a value with the same name already exists it is overwritten. If the value equals the default value it will be removed from the details bag.</summary>
		/// <param name="detailName">The name of the item to set.</param>
		/// <param name="value">The value to set. If this parameter is null or equal to defaultValue the detail is removed.</param>
		/// <param name="defaultValue">The default value. If the value is equal to this value the detail will be removed.</param>
		protected internal virtual void SetDetail<T>(string detailName, T value, T defaultValue)
		{
			if (value == null || !value.Equals(defaultValue))
			{
				SetDetail<T>(detailName, value);
			}
			else if (Details.ContainsKey(detailName))
			{
				details.Remove(detailName);
			}
		}

		/// <summary>Set a value into the <see cref="Details"/> bag. If a value with the same name already exists it is overwritten.</summary>
		/// <param name="detailName">The name of the item to set.</param>
		/// <param name="value">The value to set. If this parameter is null the detail is removed.</param>
		/// <typeparam name="T">The type of value to store in details.</typeparam>
		protected internal virtual void SetDetail<T>(string detailName, T value)
		{
			SetDetail(detailName, value, typeof(T));
		}

		/// <summary>Set a value into the <see cref="Details"/> bag. If a value with the same name already exists it is overwritten.</summary>
		/// <param name="detailName">The name of the item to set.</param>
		/// <param name="value">The value to set. If this parameter is null the detail is removed.</param>
		/// <param name="valueType">The type of value to store in details.</param>
		public virtual void SetDetail(string detailName, object value, Type valueType)
		{
			ContentDetail detail = null;
			if (Details.TryGetValue(detailName, out detail))
			{
				if (value != null && detail.ValueType.IsAssignableFrom(valueType))
				{
					// update an existing detail of same type
					detail.Value = value;
					return;
				}
			}

			if (detail != null)
				// delete detail or remove detail of wrong type
				Details.Remove(detailName);
			if (value != null)
				// add new detail
				Details.Add(detailName, N2.Details.ContentDetail.New(this, detailName, value));
		}
		#endregion

		#region GetDetailCollection
		/// <summary>Gets a named detail collection.</summary>
		/// <param name="collectionName">The name of the detail collection to get.</param>
		/// <param name="createWhenEmpty">Wether a new collection should be created if none exists. Setting this to false means null will be returned if no collection exists.</param>
		/// <returns>A new or existing detail collection or null if the createWhenEmpty parameter is false and no collection with the given name exists..</returns>
		public virtual Details.DetailCollection GetDetailCollection(string collectionName, bool createWhenEmpty)
		{
			if (DetailCollections.ContainsKey(collectionName))
				return DetailCollections[collectionName];
			else if (createWhenEmpty)
			{
				Details.DetailCollection collection = new Details.DetailCollection(this, collectionName);
				DetailCollections.Add(collectionName, collection);
				return collection;
			}
			else
				return null;
		}
		#endregion

		#region AddTo & GetChild & GetChildren

		private const int SortOrderThreshold = 9999;

		/// <summary>Adds an item to the children of this item updating its parent refernce.</summary>
		/// <param name="newParent">The new parent of the item. If this parameter is null the item is detached from the hierarchical structure.</param>
		public virtual void AddTo(ContentItem newParent)
		{
			if (Parent != null && Parent != newParent && Parent.Children.Contains(this))
				Parent.Children.Remove(this);

			url = null;
			Parent = newParent;
			
			if (newParent != null && !newParent.Children.Contains(this))
			{
				IList<ContentItem> siblings = newParent.Children;
				if (siblings.Count > 0)
				{
					int lastOrder = siblings[siblings.Count - 1].SortOrder;

					for (int i = siblings.Count - 2; i >= 0; i--)
					{
						if (siblings[i].SortOrder < lastOrder - SortOrderThreshold)
						{
							siblings.Insert(i + 1, this);
							return;
						}
						lastOrder = siblings[i].SortOrder;
					}

					if (lastOrder > SortOrderThreshold)
					{
						siblings.Insert(0, this);
						return;
					}
				}

				siblings.Add(this);
			}
		}

		/// <summary>Finds children based on the given url segments. The method supports convering the last segments into action and parameter.</summary>
		/// <param name="remainingUrl">The remaining url segments.</param>
		/// <returns>A path data object which can be empty (check using data.IsEmpty()).</returns>
		public virtual PathData FindPath(string remainingUrl)
		{
			if (remainingUrl == null)
				return PathDictionary.GetPath(this, string.Empty);

			remainingUrl = remainingUrl.TrimStart('/');

			if (remainingUrl.Length == 0)
				return PathDictionary.GetPath(this, string.Empty);

			int slashIndex = remainingUrl.IndexOf('/');
			string nameSegment = HttpUtility.UrlDecode(slashIndex < 0 ? remainingUrl : remainingUrl.Substring(0, slashIndex));
			foreach (ContentItem child in GetChildren(new NullFilter()))
			{
				if (child.IsNamed(nameSegment))
				{
					remainingUrl = slashIndex < 0 ? null : remainingUrl.Substring(slashIndex + 1);
					return child.FindPath(remainingUrl);
				}
			}

			return PathDictionary.GetPath(this, remainingUrl);
		}

    	/// <summary>Tries to get a child item with a given name. This method igonres user permissions and any trailing '.aspx' that might be part of the name.</summary>
		/// <param name="childName">The name of the child item to get.</param>
		/// <returns>The child item if it is found otherwise null.</returns>
		/// <remarks>If the method is passed an empty or null string it will return null.</remarks>
		public virtual ContentItem GetChild(string childName)
		{
			if (string.IsNullOrEmpty(childName))
				return null;

			int slashIndex = childName.IndexOf('/');
			if (slashIndex == 0) // starts with slash
			{
				if (childName.Length == 1)
					return this;
				else
					return GetChild(childName.Substring(1));
			}
			if (slashIndex > 0) // contains a slash further down
			{
				string nameSegment = HttpUtility.UrlDecode(childName.Substring(0, slashIndex));
				foreach (ContentItem child in GetChildren(new NullFilter()))
				{
					if (child.IsNamed(nameSegment))
					{
						return child.GetChild(childName.Substring(slashIndex));
					}
				}
				return null;
			}

			// no slash, only a name
			foreach (ContentItem child in GetChildren(new NullFilter()))
			{
				if (child.IsNamed(childName))
				{
					return child;
				}
			}
			return null;
		}

		/// <summary>
		/// Compares the item's name ignoring case and extension.
		/// </summary>
		/// <param name="name">The name to compare against.</param>
		/// <returns>True if the supplied name is considered the same as the item's.</returns>
        protected virtual bool IsNamed(string name)
        {
            if (Name == null)
                return false;
            return Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) 
				|| (Name + Extension).Equals(name, StringComparison.InvariantCultureIgnoreCase);
        }

		/// <summary>Gets child items the current user is allowed to access.</summary>
		/// <returns>A list of content items.</returns>
		/// <remarks>This method is used by N2 for site map providers, and for data source controls. Keep this in mind when overriding this method.</remarks>
		public virtual ItemList GetChildren()
		{
			return GetChildren(new AccessFilter());
		}

		/// <summary>Gets children the current user is allowed to access belonging to a certain zone, i.e. get only children with a certain zone name. </summary>
		/// <param name="childZoneName">The name of the zone.</param>
		/// <returns>A list of items that have the specified zone name.</returns>
		/// <remarks>This method is used by N2 when when non-page items are added to a zone on a page and in edit mode when displaying which items are placed in a certain zone. Keep this in mind when overriding this method.</remarks>
        public virtual ItemList GetChildren(string childZoneName)
        {
			return GetChildren(
                new CompositeFilter(
                    new ZoneFilter(childZoneName), 
                    new AccessFilter()));
        }

		/// <summary>Gets children applying filters.</summary>
		/// <param name="filters">The filters to apply on the children.</param>
		/// <returns>A list of filtered child items.</returns>
		public virtual ItemList GetChildren(params ItemFilter[] filters)
		{
			return GetChildren(new CompositeFilter(filters));
		}

		/// <summary>Gets children applying filters.</summary>
		/// <param name="filter">The filters to apply on the children.</param>
		/// <returns>A list of filtered child items.</returns>
		public virtual ItemList GetChildren(ItemFilter filter)
		{
			IEnumerable<ContentItem> items = VersionOf == null ? Children : VersionOf.Children;
			return new ItemList(items, filter);
		}

		#endregion

		#region IComparable & IComparable<ContentItem> Members

		int IComparable.CompareTo(object obj)
		{
			if (obj is ContentItem)
				return SortOrder - ((ContentItem)obj).SortOrder;
			else
				return 0;
		}
		int IComparable<ContentItem>.CompareTo(ContentItem other)
        {
            return SortOrder - other.SortOrder;
        }

        #endregion

        #region ICloneable Members

		object ICloneable.Clone()
		{
			return Clone(true);
		}
		
		/// <summary>Creates a copy of this item including details and authorized roles resetting ID.</summary>
		/// <param name="includeChildren">Wether this item's child items also should be cloned.</param>
		/// <returns>The cloned item with or without cloned child items.</returns>
		public virtual ContentItem Clone(bool includeChildren)
        {
			ContentItem cloned = (ContentItem)MemberwiseClone(); //Activator.CreateInstance(GetContentType(), true);

			ClearUnclonable(cloned);
			CloneDetails(this, cloned);
			CloneChildren(this, cloned, includeChildren);
			CloneAuthorizedRoles(this, cloned);

            return cloned;
        }

		#region Clone Helper Methods
		static void CloneFields(ContentItem source, ContentItem destination)
		{
			destination.title = source.title;
			destination.name = source.name;
			destination.created = source.created;
			destination.updated = source.updated;
			destination.versionIndex = source.versionIndex;
			destination.visible = source.visible;
			destination.savedBy = source.savedBy;
			destination.urlParser = source.urlParser;
			destination.url = null;
		}

		private static void ClearUnclonable(ContentItem destination)
		{
			destination.id = 0;
			destination.url = null;
			destination.parent = null;
			destination.versionOf = null;
			destination.versionIndex = 0;
			destination.ancestralTrail = null;
			destination.hashCode = null;
			destination.authorizedRoles = new List<Security.AuthorizedRole>();
			destination.children = new List<ContentItem>();
			destination.details = new Dictionary<string, Details.ContentDetail>();
			destination.detailCollections = new Dictionary<string, Details.DetailCollection>();
		}

		static void CloneAuthorizedRoles(ContentItem source, ContentItem destination)
		{
			if (source.AuthorizedRoles != null)
			{
				destination.authorizedRoles = new List<Security.AuthorizedRole>();
				foreach (Security.AuthorizedRole role in source.AuthorizedRoles)
				{
					Security.AuthorizedRole clonedRole = role.Clone();
					clonedRole.EnclosingItem = destination;
					destination.authorizedRoles.Add(clonedRole);
				}
			}
		}

		static void CloneChildren(ContentItem source, ContentItem destination, bool includeChildren)
		{
			if (includeChildren)
			{
				foreach (ContentItem child in source.Children)
				{
					ContentItem clonedChild = child.Clone(true);
					clonedChild.AddTo(destination);
				}
			}
		}

		static void CloneDetails(ContentItem source, ContentItem destination)
		{
			foreach (Details.ContentDetail detail in source.Details.Values)
			{
				if(destination.details.ContainsKey(detail.Name)) 
				{
					destination.details[detail.Name].Value = detail.Value;//.Value should behave polymorphically
				} 
				else 
				{
					ContentDetail clonedDetail = detail.Clone();
					clonedDetail.EnclosingItem = destination;
					destination.details[detail.Name] = clonedDetail;
				}
			}

			foreach (Details.DetailCollection collection in source.DetailCollections.Values)
			{
				Details.DetailCollection clonedCollection = collection.Clone();
				clonedCollection.EnclosingItem = destination;
				destination.DetailCollections[collection.Name] = clonedCollection;
			}
		} 
		#endregion


        #endregion

		#region INode Members

		/// <summary>The logical path to the node from the root node.</summary>
		public virtual string Path
		{
			get
			{
				if (VersionOf != null)
					return VersionOf.Path;

				string path = "/";
				for (ContentItem item = this; item.Parent != null; item = item.Parent)
				{
					if (item.Name != null)
						path = "/" + Uri.EscapeDataString(item.Name) + path;
					else
						path = "/" + item.ID + path;
				}
				return path;
			}
		}

		string INode.PreviewUrl
		{
			get { return Url; }
		}

		string INode.ClassNames
		{
			get
			{
				StringBuilder className = new StringBuilder();

				if (!Published.HasValue || Published > DateTime.Now)
					className.Append("unpublished ");
				else if (Published > DateTime.Now.AddDays(-1))
					className.Append("day ");
				else if (Published > DateTime.Now.AddDays(-7))
					className.Append("week ");
				else if (Published > DateTime.Now.AddMonths(-1))
					className.Append("month ");

				if (Expires.HasValue && Expires <= DateTime.Now)
					className.Append("expired ");

				if (!Visible)
					className.Append("invisible ");

				if (AuthorizedRoles != null && AuthorizedRoles.Count > 0)
					className.Append("locked ");

				return className.ToString();
			}
		}

		/// <summary>Gets whether a certain user is authorized to view this item.</summary>
		/// <param name="user">The user to check.</param>
		/// <returns>True if the item is open for all or the user has the required permissions.</returns>
		public virtual bool IsAuthorized(IPrincipal user)
		{
			if ((AlteredPermissions & N2.Security.Permission.Read) == N2.Security.Permission.None)
				return true;

			if (AuthorizedRoles == null || AuthorizedRoles.Count == 0)
				return true;

			// Iterate allowed roles to find an allowed role
			foreach (Security.Authorization auth in AuthorizedRoles)
			{
				if (auth.IsAuthorized(user))
					return true;
			}
			return false;

		}

		#region ILink Members

		string Web.ILink.Contents
		{
			get { return Title; }
		}

		string Web.ILink.ToolTip
		{
			get { return string.Empty; }
		}

		string Web.ILink.Target
		{
			get { return string.Empty; }
		}

    	#endregion
		#endregion

		#region Equals, HashCode and ToString Overrides
		/// <summary>Checks the item with another for equality.</summary>
		/// <returns>True if two items have the same ID.</returns>
		public override bool Equals(object obj)
		{
			if (this == obj) return true;
			ContentItem other = obj as ContentItem;
			return other != null && id != 0 && id == other.id;
		}

		int? hashCode;
		/// <summary>Gets a hash code based on the ID.</summary>
		/// <returns>A hash code.</returns>
		[DebuggerStepThrough]
		public override int GetHashCode()
		{
			if (!hashCode.HasValue)
				hashCode = (id > 0 ? id.GetHashCode() : base.GetHashCode());
			return hashCode.Value;
}

		/// <summary>Returns this item's name.</summary>
		/// <returns>The item's name.</returns>
		[DebuggerStepThrough]
		public override string ToString()
		{
			return Name + "#" + ID;
		}

		/// <summary>Compares two content items for equality.</summary>
		/// <param name="a">The fist item. If equality is overridden this item's method is invoked.</param>
		/// <param name="b">The second item.</param>
		/// <returns>True if the items are equal or null.</returns>
		public static bool operator ==(ContentItem a, ContentItem b)
		{
			if (System.Object.ReferenceEquals(a, b))
				return true; // If both are null, or both are same instance, return true.

			// If one is null, but not both, return false.
			if (((object)a == null) || ((object)b == null))
			{
				return false;
			}

			// Return true if the fields match:
			return a.Equals(b);
		}

		/// <summary>Compares two content items for iverse equality.</summary>
		/// <param name="a">The fist item. If equality is overridden this item's method is invoked.</param>
		/// <param name="b">The second item.</param>
		/// <returns>False if the items are equal or null.</returns>
		public static bool operator !=(ContentItem a, ContentItem b)
		{
			return !(a == b);
		}

		#endregion

		#region IUpdatable<ContentItem> Members

		void IUpdatable<ContentItem>.UpdateFrom(ContentItem source)
		{
			CloneFields(source, this);
			CloneDetails(source, this);
			ClearMissingDetails(source, this);
		}

		private void ClearMissingDetails(ContentItem source, ContentItem destination)
		{
			// remove details not present in source
			List<string> detailKeys = new List<string>(destination.Details.Keys);
			foreach(string key in detailKeys)
			{
				if (!source.Details.ContainsKey(key))
					destination.Details.Remove(key);
			}

			List<string> collectionKeys = new List<string>(destination.DetailCollections.Keys);
			foreach (string key in collectionKeys)
			{
				if (source.DetailCollections.ContainsKey(key))
				{
					// remove detail collection values not present in source
					DetailCollection destinationCollection = destination.DetailCollections[key];
					DetailCollection sourceCollection = source.DetailCollections[key];
					List<object> values = new List<object>(destinationCollection.Enumerate<object>());
					foreach(object value in values)
					{
						if(!sourceCollection.Contains(value))
							destinationCollection.Remove(value);
					}
				}
				else
					// remove detail collections not present in source
					destination.DetailCollections.Remove(key);
			}
		}

		#endregion


		#region IInterceptable Members

		public virtual Type GetContentType()
		{
			return base.GetType();
		}

		#endregion

		#region IDependentEntity<IUrlParser> Members

		void IDependentEntity<IUrlParser>.Set(IUrlParser dependency)
		{
			urlParser = dependency;
		}

		#endregion
	}
}