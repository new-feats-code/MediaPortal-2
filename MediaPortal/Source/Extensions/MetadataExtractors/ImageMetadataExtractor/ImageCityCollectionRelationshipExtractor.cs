﻿#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.Helpers;

namespace MediaPortal.Extensions.MetadataExtractors.ImageMetadataExtractor
{
  class ImageCityCollectionRelationshipExtractor : IRelationshipRoleExtractor
  {
    private static readonly Guid[] ROLE_ASPECTS = { MediaAspect.ASPECT_ID, ImageAspect.ASPECT_ID };
    private static readonly Guid[] LINKED_ROLE_ASPECTS = { ImageCollectionAspect.ASPECT_ID };

    public Guid Role
    {
      get { return ImageAspect.ROLE_IMAGE; }
    }

    public Guid[] RoleAspects
    {
      get { return ROLE_ASPECTS; }
    }

    public Guid LinkedRole
    {
      get { return ImageCollectionAspect.ROLE_IMAGE_COLLECTION; }
    }

    public Guid[] LinkedRoleAspects
    {
      get { return LINKED_ROLE_ASPECTS; }
    }

    public bool TryExtractRelationships(IDictionary<Guid, IList<MediaItemAspect>> aspects, out ICollection<IDictionary<Guid, IList<MediaItemAspect>>> extractedLinkedAspects, bool forceQuickMode)
    {
      extractedLinkedAspects = null;

      double latitude;
      if (!MediaItemAspect.TryGetAttribute(aspects, ImageAspect.ATTR_LATITUDE, out latitude))
        return false;

      double longitude;
      if (!MediaItemAspect.TryGetAttribute(aspects, ImageAspect.ATTR_LONGITUDE, out longitude))
        return false;

      string city;
      if (!MediaItemAspect.TryGetAttribute(aspects, ImageAspect.ATTR_CITY, out city))
        return false;

      string country;
      if (!MediaItemAspect.TryGetAttribute(aspects, ImageAspect.ATTR_COUNTRY, out country))
        return false;

      string state = null;
      MediaItemAspect.TryGetAttribute(aspects, ImageAspect.ATTR_STATE, out state);

      if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(country))
        return false;

      // Build the image collection MI

      ImageCollectionInfo collection = new ImageCollectionInfo()
      {
        Name = string.Format("{0}, {1}", city.Trim(), state == null ? country.Trim() : state.Trim() + ", " + country.Trim()),
        Latitude = latitude,
        Longitude = longitude,
        CollectionType = ImageCollectionAspect.TYPE_CITY
      };

      extractedLinkedAspects = new List<IDictionary<Guid, IList<MediaItemAspect>>>();
      IDictionary<Guid, IList<MediaItemAspect>> collectionAspects = new Dictionary<Guid, IList<MediaItemAspect>>();
      extractedLinkedAspects.Add(collectionAspects);
      collection.SetMetadata(collectionAspects);

      return true;
    }

    public bool TryMatch(IDictionary<Guid, IList<MediaItemAspect>> extractedAspects, IDictionary<Guid, IList<MediaItemAspect>> existingAspects)
    {
      return existingAspects.ContainsKey(ImageCollectionAspect.ASPECT_ID);
    }

    public bool TryGetRelationshipIndex(IDictionary<Guid, IList<MediaItemAspect>> aspects, IDictionary<Guid, IList<MediaItemAspect>> linkedAspects, out int index)
    {
      index = -1;

      DateTime imageDate;
      if (!MediaItemAspect.TryGetAttribute(aspects, MediaAspect.ATTR_RECORDINGTIME, out imageDate))
        return false;

      index = Convert.ToInt32((DateTime.Now - new DateTime(2000, 1, 1)).TotalSeconds);
      return true;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}