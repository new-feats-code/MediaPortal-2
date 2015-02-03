﻿#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Extensions.MetadataExtractors.NfoMetadataExtractors.Stubs;

namespace MediaPortal.Extensions.MetadataExtractors.NfoMetadataExtractors
{
  /// <summary>
  /// Base class for all nfo-file readers
  /// </summary>
  /// <remarks>
  /// We have a separate reader for the different nfo-files of all possible MediaItem types (in particular movies and series).
  /// This abstract base class contains common functionality that can be used for all types of nfo-files.
  /// </remarks>
  public abstract class NfoReaderBase
  {
    #region Consts

    /// <summary>
    /// Buffer size used for the FileStream that (asynchronously) reads the nfo-files
    /// </summary>
    private const int BUFFER_SIZE = 4096;

    #endregion

    #region Delegates

    /// <summary>
    /// Delegate used to synchronously read a <see cref="XElement"/> from a nfo-file
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read</param>
    /// <returns><c>true</c> if metadata could be read successfully from <paramref name="element"/>; else <c>false</c></returns>
    protected delegate bool TryReadElementDelegate(XElement element);

    /// <summary>
    /// Delegate used to asynchronously read a <see cref="XElement"/> from a nfo-file
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read</param>
    /// <param name="nfoDirectoryFsra"><see cref="IFileSystemResourceAccessor"/> to the parent directory of the nfo-file</param>
    /// <returns><c>true</c> if metadata could be read successfully from <paramref name="element"/>; else <c>false</c></returns>
    protected delegate Task<bool> TryReadElementAsyncDelegate(XElement element, IFileSystemResourceAccessor nfoDirectoryFsra);

    /// <summary>
    /// Delegate used to write metadata into a specific Attribute of a MediaItemAspect
    /// </summary>
    /// <param name="extractedAspectData">Dictionary of MediaItemAspects to write the Attribute to</param>
    /// <returns><c>true</c> if metadata was written to the Attribute; else <c>false</c></returns>
    protected delegate bool TryWriteAttributeDelegate(IDictionary<Guid, MediaItemAspect> extractedAspectData);

    #endregion

    #region Protected fields

    /// <summary>
    /// Debug logger
    /// </summary>
    /// <remarks>
    /// NoLogger or FileLogger depending on the respective <see cref="NfoMetadataExtractorSettingsBase"/>
    /// </remarks>
    protected ILogger DebugLogger;

    /// <summary>
    /// Unique number of the MediaItem for which this NfoReader was instantiated
    /// </summary>
    protected long MiNumber;

    /// <summary>
    /// If true, no long lasting operations such as parsing pictures are performed
    /// </summary>
    protected bool ForceQuickMode;

    /// <summary>
    /// Dictionary used to find the appropriate <see cref="TryReadElementDelegate"/> or <see cref="TryReadElementAsyncDelegate"/> by element name
    /// </summary>
    protected readonly Dictionary<XName, Delegate> SupportedElements = new Dictionary<XName, Delegate>();

    /// <summary>
    /// List of <see cref="TryWriteAttributeDelegate"/>s used to write metadata into a specific Attribute of a MediaItemAspect
    /// </summary>
    protected readonly List<TryWriteAttributeDelegate> SupportedAttributes = new List<TryWriteAttributeDelegate>();

    /// <summary>
    /// <see cref="HttpClient"/> used to download from http URLs contained in nfo-files
    /// </summary>
    protected readonly HttpClient HttpDownloadClient;

    #endregion

    #region Private fields

    /// <summary>
    /// Settings of the NfoMetadataExtractor
    /// </summary>
    /// <remarks>
    /// This abstract base class can only access the properties of the settings base class <see cref="NfoMetadataExtractorSettingsBase"/>.
    /// Properties defined in a settings class derived from <see cref="NfoMetadataExtractorSettingsBase"/> can only be accessed by the
    /// respective derived reader class.
    /// </remarks>
    private readonly NfoMetadataExtractorSettingsBase _settings;

    #endregion

    #region Ctor

    /// <summary>
    /// Initializes protected and private fields
    /// </summary>
    /// <param name="debugLogger">Debug logger to log to</param>
    /// <param name="miNumber">Unique number of the MediaItem for which the nfo-file is parsed</param>
    /// <param name="forceQuickMode">If <c>true</c>, no long lasting operations such as parsing pictures are performed</param>
    /// <param name="httpClient"><see cref="HttpClient"/> used to download from http URLs contained in nfo-files</param>
    /// <param name="settings">Settings of the NfoMetadataExtractor</param>
    protected NfoReaderBase(ILogger debugLogger, long miNumber, bool forceQuickMode, HttpClient httpClient, NfoMetadataExtractorSettingsBase settings)
    {
      DebugLogger = debugLogger;
      MiNumber = miNumber;
      ForceQuickMode = forceQuickMode;
      HttpDownloadClient = httpClient;
      _settings = settings;
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Tries to read an nfo-file into a <see cref="XDocument"/>
    /// </summary>
    /// <param name="nfoFsra"><see cref="IFileSystemResourceAccessor"/> pointing to the nfo-file</param>
    /// <returns><c>true</c> if any usable metadata was found; else <c>false</c></returns>
    public async Task<bool> TryReadMetadataAsync(IFileSystemResourceAccessor nfoFsra)
    {
      byte[] nfoBytes;
      var nfoFileWrittenToDebugLog = false;
      try
      {
        using (var nfoStream = GetAsyncStream(nfoFsra))
        {
          // For xml-files it is recommended to read them as byte array. Reason is that reading as byte array does
          // not yet consider any encoding. After that, it is recommended to use the XmlReader (instead of a StreamReader)
          // because the XmlReader first considers the "Byte Order Mark" ("BOM"). If such is not present, UTF-8 is used.
          // If the XML declaration contains an encoding attribute (which is optional), the XmlReader (contrary to the
          // StreamReader) automatically switches to the enconding specified by the XML declaration.
          nfoBytes = new byte[nfoStream.Length];
          await nfoStream.ReadAsync(nfoBytes, 0, (int)nfoStream.Length);
          if (_settings.EnableDebugLogging && _settings.WriteRawNfoFileIntoDebugLog)
            using (var nfoMemoryStream = new MemoryStream(nfoBytes))
            using (var nfoReader = new StreamReader(nfoMemoryStream, true))
            {
              var nfoString = nfoReader.ReadToEnd();
              DebugLogger.Debug("[#{0}]: Nfo-file (Encoding: {1}):{2}{3}", MiNumber, nfoReader.CurrentEncoding, Environment.NewLine, nfoString);
              nfoFileWrittenToDebugLog = true;
            }
        }
      }
      catch (Exception e)
      {
        DebugLogger.Error("[#{0}]: Cannot extract metadata; cannot read nfo-file", e, MiNumber);
        return false;
      }
      
      try
      {
        using(var memoryNfoStream = new MemoryStream(nfoBytes))
        using (var xmlReader = XmlReader.Create(memoryNfoStream))
        {
          var nfoDocument = XDocument.Load(xmlReader);
          return await TryReadNfoDocumentAsync(nfoDocument, nfoFsra);
        }
      }
      catch (Exception e)
      {
        using(var nfoMemoryStream = new MemoryStream(nfoBytes))
        using (var nfoReader = new StreamReader(nfoMemoryStream, true))
        {
          try
          {
            if (!nfoFileWrittenToDebugLog)
            {
              var nfoString = nfoReader.ReadToEnd();
              DebugLogger.Warn("[#{0}]: Cannot extract metadata; cannot parse nfo-file with XMLReader (Encoding: {1}):{2}{3}", e, MiNumber, nfoReader.CurrentEncoding, Environment.NewLine, nfoString);
            }
          }
          catch (Exception)
          {
            DebugLogger.Error("[#{0}]: Cannot extract metadata; neither XMLReader can parse nor StreamReader can read the bytes read from the nfo-file", e, MiNumber);
          }
        }
        return false;
      }
    }

    /// <summary>
    /// Tries to write the available metadata into the respective MediaItemsAspects
    /// </summary>
    /// <param name="extractedAspectData">Dictionary with MediaItemAspects into which the metadata should be written</param>
    /// <returns><c>true</c> if any metadata was written in the <param name="extractedAspectData"></param>; otherwise <c>false</c></returns>
    /// <remarks>
    /// This method was designed as a Try...-method for later, when our MDEs support priorities on Attribute level.
    /// Currently this method only returns <c>false</c>, if not metadata was found that could be written to any supported Attribute of a MediaItemAspect
    /// <param name="extractedAspectData"></param> must not be <c>null</c>. If it does not contain a MediaItemAspect, in which this method wants
    /// to store metadata, this MediaItemAspect is added to <param name="extractedAspectData"></param>.
    /// </remarks>
    public bool TryWriteMetadata(IDictionary<Guid, MediaItemAspect> extractedAspectData)
    {
      var stubObjectsLogged = false;
      var result = false;
      if (_settings.EnableDebugLogging && _settings.WriteStubObjectIntoDebugLog)
      {
        LogStubObjects();
        stubObjectsLogged = true;
      }
      foreach (var writeDelegate in SupportedAttributes)
      {
        try
        {
          result = writeDelegate.Invoke(extractedAspectData) || result;
        }
        catch (Exception e)
        {
          DebugLogger.Error("[#{0}]: Error writing metadata into the MediaItemAspects (delegate: {1})", e, MiNumber, writeDelegate);
          if (stubObjectsLogged)
            continue;
          LogStubObjects();
          stubObjectsLogged = true;
        }
      }
      return result;
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Calls the appropriate <see cref="TryReadElementDelegate"/> or <see cref="TryReadElementAsyncDelegate"/>for each element of root
    /// </summary>
    /// <param name="nfoDocument"><see cref="XDocument"/> containing the respective nfo-file</param>
    /// <param name="nfoFsra"><see cref="IFileSystemResourceAccessor"/> to the nfo-file</param>
    /// <returns><c>true</c> if any usable metadata was found; else <c>false</c></returns>
    private async Task<bool> TryReadNfoDocumentAsync(XDocument nfoDocument, IFileSystemResourceAccessor nfoFsra)
    {
      // Checks the name of the root element
      if (!CanReadNfoDocument(nfoDocument))
        return false;

      var metadataFound = false;
      
      // Create an IFileSystemResourceAccessor to the parent directory of the nfo-file 
      var nfoDirectoryResourcePath = ResourcePathHelper.Combine(nfoFsra.CanonicalLocalResourcePath, "../");
      IResourceAccessor nfoDirectoryRa;
      IFileSystemResourceAccessor nfoDirectoryFsra = null;
      if (nfoDirectoryResourcePath.TryCreateLocalResourceAccessor(out nfoDirectoryRa))
      {
        nfoDirectoryFsra = nfoDirectoryRa as IFileSystemResourceAccessor;
        if (nfoDirectoryFsra == null)
          nfoDirectoryRa.Dispose();
      }

      using (nfoDirectoryFsra)
      {
        // CanReadNfoDocument returns false if nfoRootDocument is null
        // ReSharper disable once PossibleNullReferenceException
        foreach (var element in nfoDocument.Root.Elements())
        {
          Delegate readDelegate;
          if (SupportedElements.TryGetValue(element.Name, out readDelegate))
          {
            try
            {
              if ((readDelegate is TryReadElementDelegate && (readDelegate as TryReadElementDelegate).Invoke(element)) ||
                  (readDelegate is TryReadElementAsyncDelegate && await (readDelegate as TryReadElementAsyncDelegate).Invoke(element, nfoDirectoryFsra)))
                metadataFound = true;
            }
            catch (Exception e)
            {
              DebugLogger.Error("[#{0}]: Exception while reading element {1}", e, MiNumber, element);
            }
          }
          else
            DebugLogger.Warn("[#{0}]: Unknown element {1}", MiNumber, element);
        }
      }
      return metadataFound;
    }

    /// <summary>
    /// Opens a stream for an <see cref="IFileSystemResourceAccessor"/> (if possible in async mode)
    /// </summary>
    /// <param name="nfoFsra"><see cref="IFileSystemResourceAccessor"/> to open the stream for</param>
    /// <returns>Opened stream</returns>
    /// <remarks>
    /// Our IResourceAccessor does not support opening a stream for async access, yet.
    /// We therefore use a workaround and directly access the file and open it in async mode,
    /// if nfoFsra is a ILocalFsResourceAccessor, otherwise we just use OpenRead()
    /// ToDo: Remove this when IResourceAccessor supports opening streams in async mode
    /// </remarks>
    private static Stream GetAsyncStream(IFileSystemResourceAccessor nfoFsra)
    {
      var lfsra = nfoFsra as ILocalFsResourceAccessor;
      return lfsra != null ?
        new FileStream(lfsra.LocalFileSystemPath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, true) :
        nfoFsra.OpenRead();
    }

    #endregion

    #region Protected methods

    /// <summary>
    /// Tries to read a simple string from <paramref name="element"/>.Value
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <returns>
    /// <c>null</c> if <paramref name="element"/> is null, <paramref name="element"/> has child elements,
    /// <paramref name="element"/>.Value.Trim() is null, <paramref name="element"/>.Value.Trim() is an empty string or
    /// _seetings.IgnoreStrings is not null and contains a value that (ignoring casing) exactly matches <paramref name="element"/>.Value.Trim();
    /// otherwise <paramref name="element"/>.Value.Trim()
    /// </returns>
    protected string ParseSimpleString(XElement element)
    {
      if (element == null)
        return null;
      if (element.HasElements)
      {
        DebugLogger.Warn("[#{0}]: The following element was supposed to contain a simple value, but it contains child elements: {1}", MiNumber, element);
        return null;
      }
      var result = element.Value.Trim();
      if (_settings.IgnoreStrings != null && _settings.IgnoreStrings.Contains(result, StringComparer.OrdinalIgnoreCase))
        return null;
      return String.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Tries to read a simple int? from <paramref name="element"/>.Value
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <returns>
    /// <c>null</c> if <see cref="ParseSimpleString"/> returns <c>null</c> for <paramref name="element"/>
    /// or <see cref="ParseSimpleString"/> for <paramref name="element"/> does not contain a valid <see cref="int"/> value;
    /// otherwise (int?)<paramref name="element"/>
    /// </returns>
    protected int? ParseSimpleInt(XElement element)
    {
      var intString = ParseSimpleString(element);
      if (intString == null)
        return null;
      int? result = null;
      try
      {
        result = (int?)element;
      }
      catch (Exception)
      {
        DebugLogger.Warn("[#{0}]: The following element was supposed to contain an int value, but it does not: {1}", MiNumber, element);
      }
      return result;
    }

    /// <summary>
    /// Tries to read a simple decimal? from <paramref name="element"/>.Value
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <returns>
    /// <c>null</c> if <see cref="ParseSimpleString"/> returns <c>null</c> for <paramref name="element"/>
    /// or <see cref="ParseSimpleString"/> for <paramref name="element"/> does not contain a valid <see cref="decimal"/> value;
    /// otherwise (decimal?)<paramref name="element"/>
    /// </returns>
    protected decimal? ParseSimpleDecimal(XElement element)
    {
      var decimalString = ParseSimpleString(element);
      if (decimalString == null)
        return null;
      decimal? result = null;
      try
      {
        result = (decimal?)element;
      }
      catch (Exception)
      {
        DebugLogger.Warn("[#{0}]: The following element was supposed to contain a decimal value, but it does not: {1}", MiNumber, element);
      }
      return result;
    }

    /// <summary>
    /// Tries to read a simple DateTime? from <paramref name="element"/>.Value
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <returns>
    /// <c>null</c> if <see cref="ParseSimpleString"/> returns <c>null</c> for <paramref name="element"/>
    /// or <see cref="ParseSimpleString"/> for <paramref name="element"/> does not contain a valid <see cref="DateTime"/> value;
    /// otherwise <paramref name="element"/>.Value converted to a <see cref="DateTime"/>
    /// </returns>
    /// <remarks>
    /// <paramref name="element"/>.Value can be any string that can be parsed by <see cref="DateTime"/>.TryParse or
    /// a simple four digit year. In the latter case the value will be 1 January of that year.
    /// </remarks>
    protected DateTime? ParseSimpleDateTime(XElement element)
    {
      var dateTimeString = ParseSimpleString(element);
      if (dateTimeString == null)
        return null;

      DateTime dateTime;
      if (DateTime.TryParse(dateTimeString, out dateTime))
        return dateTime;

      int year;
      if (Int32.TryParse(dateTimeString, out year))
        if (year >= 1000 && year <= 9999)
          return new DateTime(year, 1, 1);

      DebugLogger.Warn("[#{0}]: The following element was supposed to contain a DateTime value, but it does not: {1}", MiNumber, element);
      return null;
    }

    /// <summary>
    /// Tries to read a simple image from <paramref name="element"/>.Value
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="nfoDirectoryFsra"><see cref="IFileSystemResourceAccessor"/> pointing to the parent directory of the nfo-file</param>
    /// <returns>
    /// <c>null</c> if
    ///   - <see cref="ForceQuickMode"/> is <c>true</c>; or
    ///   - a call to <see cref="ParseSimpleString"/> for <paramref name="element"/> returns <c>null</c>
    ///   - <paramref name="element"/>.Value does not contain a valid and existing (absolute) http URL to an image; or
    ///   - <paramref name="element"/>.Value does contain a valid and existing (relative) file path or <paramref name="nfoDirectoryFsra"/> is <c>null</c>;
    /// otherwise the image file read as byte array.
    /// </returns>
    /// <remarks>
    /// <paramref name="element.Value"/> can be
    ///   - a file name:
    ///     <example>folder.jpg</example>
    ///     The file must then be in the same directory as the nfo-file
    ///   - a relative file path:
    ///     <example>extrafanart\fanart1.jpg</example>
    ///     <example>..\thumbs\fanart.jpg</example>
    ///     The path must be relative to the parent directory of the nfo-file
    ///   - an absolute http URL
    ///     <example>http://image.tmdb.org/t/p/original/1rre3m7WsI2QavNZD4aUa8LzzcK.jpg</example>
    /// </remarks>
    protected async Task<byte[]> ParseSimpleImageAsync(XElement element, IFileSystemResourceAccessor nfoDirectoryFsra)
    {
      if (ForceQuickMode)
        return null;

      var imageFileString = ParseSimpleString(element);
      if (imageFileString == null)
        return null;

      // First check whether it is a local file
      if (nfoDirectoryFsra != null)
      {
        var imageFsra = nfoDirectoryFsra.GetResource(imageFileString);
        if (imageFsra != null)
          using (imageFsra)
            using (var imageStream = GetAsyncStream(imageFsra))
            {
              var result = new byte[imageStream.Length];
              await imageStream.ReadAsync(result, 0, (int)imageStream.Length);
              return result;
            }
      }
      else
        DebugLogger.Error("[#{0}]: The nfo-file's parent directory's fsra could not be created", MiNumber);

      // Then check if we have a valid http URL
      Uri imageFileUri;
      if (!Uri.TryCreate(imageFileString, UriKind.Absolute, out imageFileUri) || imageFileUri.Scheme != Uri.UriSchemeHttp)
      {
        DebugLogger.Warn("[#{0}]: The following element does neither contain an exsisting file name nor a valid http URL: {1}", MiNumber, element);
        return null;
      }

      // Finally try to download the image from the internet
      try
      {
        var response = await HttpDownloadClient.GetAsync(imageFileUri);
        if (response.IsSuccessStatusCode)
          return await response.Content.ReadAsByteArrayAsync();
        DebugLogger.Warn("[#{0}]: Http status code {1} ({2}) when trying to download image file: {3}", MiNumber, (int)response.StatusCode, response.StatusCode, element);
      }
      catch (Exception e)
      {
        DebugLogger.Warn("[#{0}]: The following image file could not be downloaded: {1}", e, MiNumber, element);
      }
      return null;
    }

    /// <summary>
    /// Tries to parse one or more images from a single <see cref="XElement"/> and add them to an existing HashSet of byte[]
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="values">HashSet of byte[] to which new entries should be added</param>
    /// <param name="nfoDirectoryFsra"><see cref="IFileSystemResourceAccessor"/> pointing to the parent directory of the nfo-file</param>
    /// <returns>A HashSet of byte[] with the new and old images or <c>null</c> if there are neither old nor new values</returns>
    protected async Task<HashSet<byte[]>> ParseMultipleImagesAsync(XElement element, HashSet<byte[]> values, IFileSystemResourceAccessor nfoDirectoryFsra)
    {
      // Examples of valid elements:
      // 1:
      // <element.Name>[ImageString]</element.Name>
      // 2:
      // <element.Name>
      //   <thumb>[ImageString]</thumb>
      // </element.Name>
      // The <element.Name> element may contain multiple <thumb> child elements
      // For examples of valid [ImageString] values see the comment of NfoReaderBase.ParseSimpleImageAsync
      if (element == null)
        return values;
      var newValues = new HashSet<byte[]>();
      if (!element.HasElements)
      {
        // Example 1:
        var value = await ParseSimpleImageAsync(element, nfoDirectoryFsra);
        if (value != null)
          newValues.Add(value);
      }
      else
      {
        // Example 2:
        foreach (var childElement in element.Elements())
          if (childElement.Name == "thumb")
          {
            var value = await ParseSimpleImageAsync(childElement, nfoDirectoryFsra);
            if (value != null)
              newValues.Add(value);
          }
          else
            DebugLogger.Warn("[#{0}]: Unknown child element {1}", MiNumber, childElement);
      }

      if (!newValues.Any())
        return values;
      
      if (values == null)
        values = new HashSet<byte[]>(newValues);
      else
        foreach (var value in newValues)
          values.Add(value);
      return values;
    }

    /// <summary>
    /// Tries to parse a string which consists of one or more sub-strings separated by a particular separator character
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="values">HashSet of strings to which new entries should be added</param>
    /// <returns>
    /// A HashSet with the new and old string values or <c>null</c> if there are neither old nor new values
    /// If _settings.IgnoreStrings is not null, new values equaling (ignoring casing) one of the
    /// values in _settings.IgnoreStrings are filtered and not added to the result
    /// </returns>
    protected HashSet<string> ParseCharacterSeparatedStrings(XElement element, HashSet<String> values)
    {
      var elementContent = ParseSimpleString(element);
      if (elementContent == null)
        return values;
      List<String> separatedStrings;
      if (_settings.SeparatorCharacters != null && _settings.SeparatorCharacters.Count != 0)
        separatedStrings = elementContent.Split(_settings.SeparatorCharacters.ToArray()).Select(str => str.Trim()).Where(str => !String.IsNullOrEmpty(str)).ToList();
      else
        separatedStrings = new List<string> { elementContent };
      if (_settings.IgnoreStrings != null)
        separatedStrings = separatedStrings.Where(str => !_settings.IgnoreStrings.Contains(str, StringComparer.OrdinalIgnoreCase)).ToList();
      if (!separatedStrings.Any())
        return values;
      if (values == null)
        values = new HashSet<String>(separatedStrings, StringComparer.OrdinalIgnoreCase);
      else
        foreach (var simpleString in separatedStrings)
          values.Add(simpleString);
      return values;
    }

    /// <summary>
    /// Tries to read an attribute with a given <paramref name="attributeName"/> as string from <paramref name="element"/>
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="attributeName">Name of the attribute</param>
    /// <returns>
    /// <c>null</c> if <paramref name="element"/> is null, an attribute with the given
    /// <param name="attributeName"></param> does not exist or contains an empty string;
    /// otherwise the (trimmed) value of the given attribute as string
    /// </returns>
    protected string ParseStringAttribute(XElement element, string attributeName)
    {
      if (element == null)
        return null;
      var attribute = element.Attribute(attributeName);
      if (attribute == null)
        return null;
      var result = attribute.Value.Trim();
      return String.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Tries to read an attribute with a given <paramref name="attributeName"/> as int? from <paramref name="element"/>
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="attributeName">Name of the attribute</param>
    /// <returns>
    /// <c>null</c> if <paramref name="element"/> is null, an attribute with the given
    /// <param name="attributeName"></param> does not exist or does not contain a valid int value;
    /// otherwise the value of the given attribute as int?
    /// </returns>
    protected int? ParseIntAttribute(XElement element, string attributeName)
    {
      if (element == null)
        return null;
      var attribute = element.Attribute(attributeName);
      if (attribute == null)
        return null;
      int? result = null;
      try
      {
        result = (int?)attribute;
      }
      catch (Exception)
      {
        DebugLogger.Warn("[#{0}]: The attribute '{1}' in the following element was supposed to contain an int value, but it does not: {2}", MiNumber, attributeName, element);
      }
      return result;
    }

    /// <summary>
    /// Tries to parse a <see cref="PersonStub"/> object
    /// </summary>
    /// <param name="element"><see cref="XElement"/> to read from</param>
    /// <param name="nfoDirectoryFsra"><see cref="IFileSystemResourceAccessor"/> pointing to the parent directory of the nfo-file</param>
    /// <returns>
    /// The filled <see cref="PersonStub"/> object or <c>null</c> if
    /// - element is null
    /// - element does not contain child elements
    /// - element does not contain a child element with the name "name" or such child element is empty or contains a value from _settings.IgnoreStrings
    /// </returns>
    protected async Task<PersonStub> ParsePerson(XElement element, IFileSystemResourceAccessor nfoDirectoryFsra)
    {
      // Example of a valid element:
      // <[ElementName]>
      //   <name>John Pyper-Ferguson</name>
      //   <role>Le père dans le minibus</role>
      //   <order>1</order>
      //   <thumb>http://site.com/MakenzieVega-35443.jpg</thumb>
      //   <imdb>nmXXXXXXX</imdb>
      //   <birthdate>01-01-2000</birthdate>
      //   <birthplace></birthplace>
      //   <deathdate>12-12-2050</deathdate>
      //   <deathplace></deathplace>
      //   <minibiography></minibiography>
      //   <biography></biography>
      // </[ElementName]>
      // The <name> child element is mandatory, all other child elements are optional
      if (element == null)
        return null;
      if (!element.HasElements)
      {
        DebugLogger.Warn("[#{0}]: The following element was supposed to contain a person's data in child elements, but it doesn't contain child elements: {1}", MiNumber, element);
        return null;
      }
      var value = new PersonStub();
      if ((value.Name = ParseSimpleString(element.Element("name"))) == null)
        return null;
      value.Role = ParseSimpleString(element.Element("role"));
      value.Order = ParseSimpleInt(element.Element("order"));
      value.Thumb = await ParseSimpleImageAsync(element.Element("thumb"), nfoDirectoryFsra);
      value.ImdbId = ParseSimpleString(element.Element("imdb"));
      value.Birthdate = ParseSimpleDateTime(element.Element("birthdate"));
      value.Birthplace = ParseSimpleString(element.Element("birthplace"));
      value.Deathdate = ParseSimpleDateTime(element.Element("deathdate"));
      value.Deathplace = ParseSimpleString(element.Element("deathplace"));
      value.MiniBiography = ParseSimpleString(element.Element("minibiography"));
      value.Biography = ParseSimpleString(element.Element("biography"));
      return value;
    }

    #endregion

    #region Abstract methods

    /// <summary>
    /// Checks whether <see cref="nfoDocument"/>.Root.Name has a value that the derived NfoReader can understand
    /// </summary>
    /// <param name="nfoDocument"><see cref="XDocument"/> to check</param>
    /// <returns><c>true</c> if the derived NfoReader can understand the nfoDocument; otherwise <c>false</c></returns>
    protected abstract bool CanReadNfoDocument(XDocument nfoDocument);

    /// <summary>
    /// Writes all StubObjects including their metadata into the debug log
    /// </summary>
    protected abstract void LogStubObjects();

    #endregion
  }
}
