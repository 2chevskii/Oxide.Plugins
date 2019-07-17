using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

//Refused by uMod
//Has some problems, related to owner of the permission (No ability to pass plugin object, that sucks), need to think on that

namespace Oxide.Plugins
{
	[Info("Permission Helper", "2CHEVSKII", "0.1.0")]
	[Description("Makes managing permissions easier than ever before")]
	internal class PermissionHelper : CovalencePlugin
	{

		#region [Fields]
		private const string cmdSave = "permissionhelper.save";
		private const string cmdLoad = "permissionhelper.load";
		private const string dataFileName = "PermissionHelper_DATA";
		private const string dataBackupName = "PermissionHelper_BACKUP";
		private DynamicConfigFile fileManager;
		private Permissions permissionsObj;
		#endregion

		#region [Messages]
		private const string prefix = "[PERMISSION HELPER]";
		private const string newData = "New data file created";
		private const string permsLoaded = "Permission list loaded";
		private const string permsSaved = "Permission list saved";
		private const string permsRestoredFromCorrupt = "Permission list restored after corruption";
		private const string plugError = "Plugin error - {0}";
		private const string cantUseCommand = "You are not allowed to use this command";
		#endregion

		#region [Hooks]
		private void Loaded() => Initialize();
		private void Unload() => ExportPermissions();
		[Command(cmdLoad)]
		private void CmdLoad(IPlayer player, string command, string[] args) => LoadPermissions(player ?? null);
		[Command(cmdSave)]
		private void CmdSave(IPlayer player, string command, string[] args) => ExportPermissions(player ?? null);
		#endregion

		#region [Plugin body]
		private void ReloadData(bool corrupt = false) => fileManager = corrupt ? Interface.Oxide.DataFileSystem.GetFile(dataBackupName) : Interface.Oxide.DataFileSystem.GetFile(dataFileName);
		private void Initialize()
		{
			lang.RegisterMessages(new Dictionary<string, string> {
				[prefix] = prefix,
				[newData] = newData,
				[permsLoaded] = permsLoaded,
				[permsSaved] = permsSaved,
				[permsRestoredFromCorrupt] = permsRestoredFromCorrupt,
				[plugError] = plugError,
				[cantUseCommand] = cantUseCommand
			}, this, "en");
			LoadPermissions();
		}

		private void LoadPermissions(IPlayer invoker = null)
		{
			if(invoker != null && !invoker.IsAdmin)
			{
				invoker.Reply(lang.GetMessage(cantUseCommand, this, invoker.Id), lang.GetMessage(prefix, this, invoker.Id));
				return;
			}

			ReloadData();

			try
			{
				permissionsObj = fileManager.ReadObject<Permissions>();
			}
			catch(Exception exception)
			{
				PrintError(plugError, exception.Message);
				ReloadData(true);
				permissionsObj = fileManager.ReadObject<Permissions>();
				PrintWarning(lang.GetMessages(lang.GetServerLanguage(), this)[permsRestoredFromCorrupt]);
			}

			if(permissionsObj.groupPermissions == null || permissionsObj.userPermissions == null || permissionsObj.allPermissions == null)
				PrintWarning(lang.GetMessages(lang.GetServerLanguage(), this)[newData]);

			NullChecker();

			foreach(string group in permissionsObj.groupPermissions.Keys)
			{
				permissionsObj.groupPermissions.TryGetValue(group, out string[] groupPermArray);
				foreach(string _permission in permission.GetGroupPermissions(group))
				{
					permission.RevokeGroupPermission(group, _permission);
				}
				foreach(string grouppermission in groupPermArray)
				{
					permission.GrantGroupPermission(group, grouppermission, null);
				}
			}

			foreach(string user in permissionsObj.userPermissions.Keys)
			{
				string _useridstring = user.Split('(')[0];

				permissionsObj.userPermissions.TryGetValue(user, out string[] userPermArray);

				foreach(string _permission in permission.GetUserPermissions(_useridstring))
				{
					permission.RevokeUserPermission(_useridstring, _permission);
				}

				foreach(string userpermission in userPermArray)
				{
					permission.GrantUserPermission(_useridstring, userpermission, null);
				}
			}

			PrintWarning(lang.GetMessages(lang.GetServerLanguage(), this)[permsLoaded]);

			ExportPermissions(message: false);

		}
		private void ExportPermissions(IPlayer invoker = null, bool message = true)
		{
			if(invoker != null && !invoker.IsAdmin)
			{
				invoker.Reply(lang.GetMessage(cantUseCommand, this, invoker.Id));
				return;
			}

			NullChecker();

			permissionsObj.groupPermissions.Clear();
			permissionsObj.userPermissions.Clear();

			try
			{
				foreach(string group in permission.GetGroups())
				{
					permissionsObj.groupPermissions.Add(group, permission.GetGroupPermissions(group));
				}

				foreach(string _permission in permission.GetPermissions())
				{
					foreach(string user in permission.GetPermissionUsers(_permission))
					{
						string _useridstring = user.Split('(')[0];
						if(!permissionsObj.userPermissions.ContainsKey(user))
							permissionsObj.userPermissions.Add(user, permission.GetUserPermissions(_useridstring));
					}
				}

				permissionsObj.allPermissions = permission.GetPermissions();

				ReloadData();

				fileManager.WriteObject(permissionsObj);
				ReloadData(true);

				fileManager.WriteObject(permissionsObj);
				if(message)
					PrintWarning(lang.GetMessages(lang.GetServerLanguage(), this)[permsSaved]);

			}
			catch(Exception exception)
			{
				PrintError(lang.GetMessages(lang.GetServerLanguage(), this)[plugError], exception.Message);
			}
		}

		private void NullChecker()
		{
			if(permissionsObj == null)
				permissionsObj = new Permissions();
			if(permissionsObj.groupPermissions == null)
				permissionsObj.groupPermissions = new Dictionary<string, string[]>();
			if(permissionsObj.userPermissions == null)
				permissionsObj.userPermissions = new Dictionary<string, string[]>();
		}

		#endregion

		#region [Nested classes]
		private class Permissions
		{
			[JsonProperty(PropertyName = "Group permissions:")]
			internal Dictionary<string, string[]> groupPermissions;

			[JsonProperty(PropertyName = "Individual permissions:")]
			internal Dictionary<string, string[]> userPermissions;

			[JsonProperty(PropertyName = "Permissions, available on Your server : DO NOT MODIFY")]
			internal string[] allPermissions;
		}
		#endregion

	}
}
