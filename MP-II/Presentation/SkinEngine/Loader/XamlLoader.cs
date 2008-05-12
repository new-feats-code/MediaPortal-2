#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Reflection;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using Presentation.SkinEngine.XamlParser;
using Presentation.SkinEngine.Controls.Visuals;
using Presentation.SkinEngine.ElementRegistrations;

namespace Presentation.SkinEngine.Loader
{
  using System.Collections.Generic;

  /// <summary>
  /// This is the loader class for XAML files. It uses a XAML parser to read the
  /// structure and builds the visual elements tree for the file.
  /// </summary>              
  public class XamlLoader
  {
    /// <summary>
    /// XAML namespace for the MediaPortal Skin Engine visual's class library.
    /// </summary>
    public const string NS_MEDIAPORTAL_MSE_URI = "www.team-mediaportal.com/2008/mse/directx";

    /// <summary>
    /// Loads the specified skin file and returns the root UIElement.
    /// </summary>
    /// <param name="skinFile">The XAML skin file.</param>
    /// <returns><see cref="UIElement"/> descendant corresponding to the root element in the
    /// specified skin file.</returns>
    public object Load(string skinFile)
    {
      // FIXME: rework the XAML file lookup mechanism
      string fullFileName = String.Format(@"skin\{0}\{1}", SkinContext.SkinName, skinFile);
      if (System.IO.File.Exists(skinFile))
      {
        fullFileName = skinFile;
      }
      Parser parser = new Parser(fullFileName, parser_ImportNamespace, parser_GetEventHandler);
      parser.SetCustomTypeConverter(MseTypeConverter.ConvertType);
      DateTime dt = DateTime.Now;
      object obj = parser.Parse();
      TimeSpan ts = DateTime.Now - dt;
      ServiceScope.Get<ILogger>().Info("XAML file {0} loaded in {1} msec", skinFile, ts.TotalMilliseconds);
      return obj;
    }

    static INamespaceHandler parser_ImportNamespace(IParserContext context, string namespaceURI)
    {
      if (namespaceURI == NS_MEDIAPORTAL_MSE_URI)
        return new MseNamespaceHandler();
      else
        throw new XamlNamespaceNotSupportedException("XAML namespace '{0}' is not supported by the MediaPortal skin engine", namespaceURI);
    }

    static Delegate parser_GetEventHandler(IParserContext context, MethodInfo signature, string value)
    {
      throw new XamlBindingException("GetEventHandler delegate implementation not supported yet");
    }
  }
}
