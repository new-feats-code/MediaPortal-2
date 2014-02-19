#region Copyright (C) 2007-2014 Team MediaPortal

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
using System.Data;
using MediaPortal.Common;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.PathManager;
using MediaPortal.Backend.Database;
using MediaPortal.Utilities;

namespace MediaPortal.Backend.Services.MediaLibrary
{
  /// <summary>
  /// Creates SQL commands for the communication with the (static part of the) MediaLibrary subschema. Commands for the
  /// dynamic part (i.e. concerning MIA types) are generated by class <see cref="MIA_Management"/>.
  /// </summary>
  public class MediaLibrary_SubSchema
  {
    #region Consts

    public const string SUBSCHEMA_NAME = "MediaLibrary";

    public const int EXPECTED_SCHEMA_VERSION_MAJOR = 1;
    public const int EXPECTED_SCHEMA_VERSION_MINOR = 1;

    internal const string MEDIA_ITEMS_TABLE_NAME = "MEDIA_ITEMS";
    internal const string MEDIA_ITEMS_ITEM_ID_COL_NAME = "MEDIA_ITEM_ID";

    #endregion

    public static string SubSchemaScriptDirectory
    {
      get
      {
        IPathManager pathManager = ServiceRegistration.Get<IPathManager>();
        return pathManager.GetPath(@"<APPLICATION_ROOT>\Scripts\");
      }
    }

    public static IDbCommand SelectAllMediaItemAspectMetadataCommand(ITransaction transaction,
        out int aspectIdIndex, out int serializationIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT MIAM_ID, MIAM_SERIALIZATION FROM MIA_TYPES";

      aspectIdIndex = 0;
      serializationIndex = 1;
      return result;
    }

    public static IDbCommand CreateMediaItemAspectMetadataCommand(ITransaction transaction, Guid miamId,
        string name, string serialization)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO MIA_TYPES (MIAM_ID, NAME, CREATION_DATE, MIAM_SERIALIZATION) VALUES (@MIAM_ID, @NAME, @CREATION_DATE, @MIAM_SERIALIZATION)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "MIAM_ID", miamId, typeof(Guid));
      database.AddParameter(result, "NAME", name, typeof(string));
      database.AddParameter(result, "CREATION_DATE", DateTime.Now, typeof(DateTime));
      database.AddParameter(result, "MIAM_SERIALIZATION", serialization, typeof(string));
      return result;
    }

    public static IDbCommand SelectMIANameAliasesCommand(ITransaction transaction,
        out int aspectIdIndex, out int identifierIndex, out int dbObjectNameIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT MIAM_ID, IDENTIFIER, DATABASE_OBJECT_NAME FROM MIA_NAME_ALIASES";

      aspectIdIndex = 0;
      identifierIndex = 1;
      dbObjectNameIndex = 2;
      return result;
    }

    public static IDbCommand CreateMIANameAliasCommand(ITransaction transaction, Guid aspectId,
        string identifier, string dbObjectName)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO MIA_NAME_ALIASES (MIAM_ID, IDENTIFIER, DATABASE_OBJECT_NAME) VALUES (@MIAM_ID, @IDENTIFIER, @DATABASE_OBJECT_NAME)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "MIAM_ID", aspectId, typeof(Guid));
      database.AddParameter(result, "IDENTIFIER", identifier, typeof(string));
      database.AddParameter(result, "DATABASE_OBJECT_NAME", dbObjectName, typeof(string));
      return result;
    }

    public static IDbCommand DeleteMediaItemAspectMetadataCommand(ITransaction transaction, Guid aspectId)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "DELETE FROM MIA_TYPES WHERE MIAM_ID=@MIAM_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "MIAM_ID", aspectId, typeof(Guid));
      return result;
    }

    public static IDbCommand SelectShareIdCommand(ITransaction transaction,
        string systemId, ResourcePath baseResourcePath, out int shareIdIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT SHARE_ID FROM SHARES WHERE SYSTEM_ID=@SYSTEM_ID AND BASE_RESOURCE_PATH=@BASE_RESOURCE_PATH";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SYSTEM_ID", systemId, typeof(string));
      database.AddParameter(result, "BASE_RESOURCE_PATH", baseResourcePath.Serialize(), typeof(string));

      shareIdIndex = 0;
      return result;
    }

    public static IDbCommand SelectSharesCommand(ITransaction transaction, out int shareIdIndex, out int systemIdIndex,
        out int baseResourcePathIndex, out int shareNameIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT SHARE_ID, SYSTEM_ID, BASE_RESOURCE_PATH, NAME FROM SHARES";

      shareIdIndex = 0;
      systemIdIndex = 1;
      baseResourcePathIndex = 2;
      shareNameIndex = 3;
      return result;
    }

    public static IDbCommand SelectShareByIdCommand(ITransaction transaction, Guid shareId, out int systemIdIndex,
        out int baseResourcePathIndex, out int shareNameIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT SYSTEM_ID, BASE_RESOURCE_PATH, NAME FROM SHARES WHERE SHARE_ID=@SHARE_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));

      systemIdIndex = 0;
      baseResourcePathIndex = 1;
      shareNameIndex = 2;
      return result;
    }

    public static IDbCommand SelectSharesBySystemCommand(ITransaction transaction, string systemId,
        out int shareIdIndex, out int systemIdIndex, out int baseResourcePathIndex, out int shareNameIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT SHARE_ID, SYSTEM_ID, BASE_RESOURCE_PATH, NAME FROM SHARES WHERE SYSTEM_ID=@SYSTEM_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SYSTEM_ID", systemId, typeof(string));

      shareIdIndex = 0;
      systemIdIndex = 1;
      baseResourcePathIndex = 2;
      shareNameIndex = 3;
      return result;
    }

    public static IDbCommand InsertShareCommand(ITransaction transaction, Guid shareId, string systemId,
        ResourcePath baseResourcePath, string shareName)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO SHARES (SHARE_ID, NAME, SYSTEM_ID, BASE_RESOURCE_PATH) VALUES (@SHARE_ID, @NAME, @SYSTEM_ID, @BASE_RESOURCE_PATH)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));
      database.AddParameter(result, "NAME", shareName, typeof(string));
      database.AddParameter(result, "SYSTEM_ID", systemId, typeof(string));
      database.AddParameter(result, "BASE_RESOURCE_PATH", baseResourcePath.Serialize(), typeof(string));
      return result;
    }

    public static IDbCommand SelectShareCategoriesCommand(ITransaction transaction, Guid shareId, out int categoryIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT CATEGORYNAME FROM SHARES_CATEGORIES WHERE SHARE_ID=@SHARE_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));

      categoryIndex = 0;
      return result;
    }

    public static IDbCommand InsertShareCategoryCommand(ITransaction transaction, Guid shareId, string mediaCategory)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO SHARES_CATEGORIES (SHARE_ID, CATEGORYNAME) VALUES (@SHARE_ID, @CATEGORYNAME)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));
      database.AddParameter(result, "CATEGORYNAME", mediaCategory, typeof(string));
      return result;
    }

    public static IDbCommand DeleteShareCategoryCommand(ITransaction transaction, Guid shareId, string mediaCategory)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "DELETE FROM SHARES_CATEGORIES WHERE SHARE_ID=@SHARE_ID AND CATEGORYNAME=@CATEGORYNAME";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));
      database.AddParameter(result, "CATEGORYNAME", mediaCategory, typeof(string));
      return result;
    }

    public static IDbCommand UpdateShareCommand(ITransaction transaction, Guid shareId, ResourcePath baseResourcePath,
        string shareName)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "UPDATE SHARES set NAME=@NAME, BASE_RESOURCE_PATH=@BASE_RESOURCE_PATH WHERE SHARE_ID=@SHARE_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "NAME", shareName, typeof(string));
      database.AddParameter(result, "BASE_RESOURCE_PATH", baseResourcePath.Serialize(), typeof(string));
      database.AddParameter(result, "SHARE_ID", shareId, typeof(Guid));

      return result;
    }

    public static IDbCommand DeleteSharesCommand(ITransaction transaction, IEnumerable<Guid> shareIds)
    {
      IDbCommand result = transaction.CreateCommand();

      ICollection<string> placeholders = new List<string>();
      int ct = 0;
      ISQLDatabase database = transaction.Database;
      foreach (Guid shareId in shareIds)
      {
        string bindVar = "ID" + ct++;
        database.AddParameter(result, bindVar, shareId, typeof(Guid));
        placeholders.Add("@" + bindVar);
      }
      result.CommandText = "DELETE FROM SHARES WHERE SHARE_ID in (" + StringUtils.Join(", ", placeholders) + ")";

      return result;
    }

    public static IDbCommand DeleteSharesOfSystemCommand(ITransaction transaction, string systemId)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "DELETE FROM SHARES WHERE SYSTEM_ID = @SYSTEM_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "SYSTEM_ID", systemId, typeof(string));
      return result;
    }

    public static IDbCommand InsertMediaItemCommand(ITransaction transaction, out Guid mediaItemId)
    {
      IDbCommand result = transaction.CreateCommand();

      result.CommandText = "INSERT INTO " + MEDIA_ITEMS_TABLE_NAME + " (" + MEDIA_ITEMS_ITEM_ID_COL_NAME + ") VALUES (@ITEM_ID)";
      mediaItemId = Guid.NewGuid();
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "ITEM_ID", mediaItemId, typeof(Guid));

      return result;
    }

    public static IDbCommand SelectPlaylistsCommand(ITransaction transaction, out int playlistIdIndex, out int playlistNameIndex,
        out int playlistTypeIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT PLAYLIST_ID, NAME, PLAYLIST_TYPE FROM PLAYLISTS";

      playlistIdIndex = 0;
      playlistNameIndex = 1;
      playlistTypeIndex = 2;
      return result;
    }

    public static IDbCommand SelectPlaylistsCommand(ITransaction transaction, out int playlistIdIndex, out int playlistNameIndex,
        out int playlistTypeIndex, out int playlistItemsCountIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT PL.PLAYLIST_ID, PL.NAME, PL.PLAYLIST_TYPE, COUNT(PC.MEDIA_ITEM_ID) " +
          "FROM PLAYLISTS PL INNER JOIN PLAYLIST_CONTENTS PC ON PL.PLAYLIST_ID=PC.PLAYLIST_ID " +
          "GROUP BY PL.PLAYLIST_ID, PL.NAME, PL.PLAYLIST_TYPE";

      playlistIdIndex = 0;
      playlistNameIndex = 1;
      playlistTypeIndex = 2;
      playlistItemsCountIndex = 3;
      return result;
    }

    public static IDbCommand SelectPlaylistIdentificationDataCommand(ITransaction transaction, Guid playlistId,
        out int playlistNameIndex, out int playlistTypeIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT NAME, PLAYLIST_TYPE FROM PLAYLISTS WHERE PLAYLIST_ID=@PLAYLIST_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "PLAYLIST_ID", playlistId, typeof(Guid));

      playlistNameIndex = 0;
      playlistTypeIndex = 1;
      return result;
    }

    public static IDbCommand InsertPlaylistCommand(ITransaction transaction, Guid playlistId, string playlistName,
        string playlistType)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO PLAYLISTS (PLAYLIST_ID, NAME, PLAYLIST_TYPE) VALUES (@PLAYLIST_ID, @NAME, @PLAYLIST_TYPE)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "PLAYLIST_ID", playlistId, typeof(Guid));
      database.AddParameter(result, "NAME", playlistName, typeof(string));
      database.AddParameter(result, "PLAYLIST_TYPE", playlistType, typeof(string));

      return result;
    }

    public static IDbCommand AddMediaItemToPlaylistCommand(ITransaction transaction, Guid playlistId, int playlistPosition,
        Guid mediaItemId)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "INSERT INTO PLAYLIST_CONTENTS (PLAYLIST_ID, PLAYLIST_POS, MEDIA_ITEM_ID) VALUES (@PLAYLIST_ID, @PLAYLIST_POS, @MEDIA_ITEM_ID)";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "PLAYLIST_ID", playlistId, typeof(Guid));
      database.AddParameter(result, "PLAYLIST_POS", playlistPosition, typeof(int));
      database.AddParameter(result, "MEDIA_ITEM_ID", mediaItemId, typeof(Guid));

      return result;
    }

    public static IDbCommand DeletePlaylistCommand(ITransaction transaction, Guid playlistId)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "DELETE FROM PLAYLISTS WHERE PLAYLIST_ID=@PLAYLIST_ID";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "PLAYLIST_ID", playlistId, typeof(Guid));

      return result;
    }

    public static IDbCommand SelectPlaylistContentsCommand(ITransaction transaction, Guid playlistId, out int mediaItemIdIndex)
    {
      IDbCommand result = transaction.CreateCommand();
      result.CommandText = "SELECT MEDIA_ITEM_ID FROM PLAYLIST_CONTENTS WHERE PLAYLIST_ID=@PLAYLIST_ID ORDER BY PLAYLIST_POS";
      ISQLDatabase database = transaction.Database;
      database.AddParameter(result, "PLAYLIST_ID", playlistId, typeof(Guid));

      mediaItemIdIndex = 0;
      return result;
    }
  }
}
