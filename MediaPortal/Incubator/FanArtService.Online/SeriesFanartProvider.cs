#region Copyright (C) 2007-2015 Team MediaPortal

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
using System.Linq;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess;
using MediaPortal.Extensions.OnlineLibraries;
using MediaPortal.Extensions.UserServices.FanArtService.Interfaces;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Backend.MediaLibrary;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.Helpers;

namespace MediaPortal.Extensions.UserServices.FanArtService
{
  public class SeriesFanartProvider : IFanArtProvider
  {
    private static readonly Guid[] NECESSARY_MIAS = { ProviderResourceAspect.ASPECT_ID, ExternalIdentifierAspect.ASPECT_ID };
    private static readonly Guid[] OPTIONAL_MIAS = { SeriesAspect.ASPECT_ID, SeasonAspect.ASPECT_ID, EpisodeAspect.ASPECT_ID, PersonAspect.ASPECT_ID, CharacterAspect.ASPECT_ID, CompanyAspect.ASPECT_ID };

    private static Dictionary<FanArtConstants.FanArtMediaType, string> fanArtScopeMap = new Dictionary<FanArtConstants.FanArtMediaType, string>()
    {
      { FanArtConstants.FanArtMediaType.Episode, FanArtScope.Episode },
      { FanArtConstants.FanArtMediaType.Series, FanArtScope.Series },
      { FanArtConstants.FanArtMediaType.SeriesSeason, FanArtScope.Season },
      { FanArtConstants.FanArtMediaType.Actor, FanArtScope.Actor },
      { FanArtConstants.FanArtMediaType.Character, FanArtScope.Character },
      { FanArtConstants.FanArtMediaType.Director, FanArtScope.Director },
      { FanArtConstants.FanArtMediaType.Writer, FanArtScope.Writer },
      { FanArtConstants.FanArtMediaType.Company, FanArtScope.Company },
      { FanArtConstants.FanArtMediaType.Network, FanArtScope.Network },
    };

    private static Dictionary<FanArtConstants.FanArtType, string> fanArtTypeMap = new Dictionary<FanArtConstants.FanArtType, string>()
    {
      { FanArtConstants.FanArtType.Banner, FanArtType.Banners },
      { FanArtConstants.FanArtType.ClearArt, FanArtType.ClearArt },
      { FanArtConstants.FanArtType.DiscArt, FanArtType.DiscArt },
      { FanArtConstants.FanArtType.FanArt, FanArtType.Backdrops },
      { FanArtConstants.FanArtType.Logo, FanArtType.Logos },
      { FanArtConstants.FanArtType.Poster, FanArtType.Posters },
      { FanArtConstants.FanArtType.Thumbnail, FanArtType.Thumbnails },
    };

    /// <summary>
    /// Gets a list of <see cref="FanArtImage"/>s for a requested <paramref name="mediaType"/>, <paramref name="fanArtType"/> and <paramref name="name"/>.
    /// The name can be: Series name, Actor name, Artist name depending on the <paramref name="mediaType"/>.
    /// </summary>
    /// <param name="mediaType">Requested FanArtMediaType</param>
    /// <param name="fanArtType">Requested FanArtType</param>
    /// <param name="name">Requested name of Series, Actor, Artist...</param>
    /// <param name="maxWidth">Maximum width for image. <c>0</c> returns image in original size.</param>
    /// <param name="maxHeight">Maximum height for image. <c>0</c> returns image in original size.</param>
    /// <param name="singleRandom">If <c>true</c> only one random image URI will be returned</param>
    /// <param name="result">Result if return code is <c>true</c>.</param>
    /// <returns><c>true</c> if at least one match was found.</returns>
    public bool TryGetFanArt(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, string name, int maxWidth, int maxHeight, bool singleRandom, out IList<IResourceLocator> result)
    {
      result = null;

      if (!fanArtScopeMap.ContainsKey(mediaType) || !fanArtTypeMap.ContainsKey(fanArtType))
        return false;

      if (string.IsNullOrWhiteSpace(name))
        return false;

      Guid mediaItemId;
      if (Guid.TryParse(name, out mediaItemId) == false)
        return false;

      IMediaLibrary mediaLibrary = ServiceRegistration.Get<IMediaLibrary>(false);
      if (mediaLibrary == null)
        return false;

      IFilter filter = new MediaItemIdFilter(mediaItemId);
      IList<MediaItem> items = mediaLibrary.Search(new MediaItemQuery(NECESSARY_MIAS, OPTIONAL_MIAS, filter), false);
      if (items == null || items.Count == 0)
        return false;

      MediaItem mediaItem = items.First();
      List<string> fanArtFiles = new List<string>();
      object infoObject = null;
      if (mediaType == FanArtConstants.FanArtMediaType.Actor || mediaType == FanArtConstants.FanArtMediaType.Director || mediaType == FanArtConstants.FanArtMediaType.Writer)
        infoObject = new PersonInfo().FromMetadata(mediaItem.Aspects);
      else if (mediaType == FanArtConstants.FanArtMediaType.Character)
        infoObject = new CharacterInfo().FromMetadata(mediaItem.Aspects);
      else if (mediaType == FanArtConstants.FanArtMediaType.Company || mediaType == FanArtConstants.FanArtMediaType.Network)
        infoObject = new CompanyInfo().FromMetadata(mediaItem.Aspects);
      else if (mediaType == FanArtConstants.FanArtMediaType.Series)
        infoObject = new SeriesInfo().FromMetadata(mediaItem.Aspects);
      else if (mediaType == FanArtConstants.FanArtMediaType.SeriesSeason)
        infoObject = new SeasonInfo().FromMetadata(mediaItem.Aspects);
      else if (mediaType == FanArtConstants.FanArtMediaType.Episode)
        infoObject = new EpisodeInfo().FromMetadata(mediaItem.Aspects);

      fanArtFiles.AddRange(SeriesTheMovieDbMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
      fanArtFiles.AddRange(SeriesTvDbMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
      fanArtFiles.AddRange(SeriesTvMazeMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
      fanArtFiles.AddRange(SeriesFanArtTvMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));

      if (fanArtFiles.Count == 0 && mediaType == FanArtConstants.FanArtMediaType.SeriesSeason &&
        (fanArtType == FanArtConstants.FanArtType.Banner || fanArtType == FanArtConstants.FanArtType.Poster))
      {
        mediaType = FanArtConstants.FanArtMediaType.Series;
        infoObject = new SeriesInfo().FromMetadata(mediaItem.Aspects);
        fanArtFiles.AddRange(SeriesTheMovieDbMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
        fanArtFiles.AddRange(SeriesTvDbMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
        fanArtFiles.AddRange(SeriesTvMazeMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
        fanArtFiles.AddRange(SeriesFanArtTvMatcher.Instance.GetFanArtFiles(infoObject, fanArtScopeMap[mediaType], fanArtTypeMap[fanArtType]));
      }

      List<IResourceLocator> files = new List<IResourceLocator>();
      try
      {
        files.AddRange(fanArtFiles
              .Select(fileName => new ResourceLocator(ResourcePath.BuildBaseProviderPath(LocalFsResourceProviderBase.LOCAL_FS_RESOURCE_PROVIDER_ID, fileName)))
              );
        result = files;
        return result.Count > 0;
      }
      catch (Exception) { }
      return false;
    }
  }
}
