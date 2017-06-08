using Sitecore.Configuration;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Pipelines.Save;
using Sitecore.Web;
using System;

namespace Sitecore.Support.Pipelines
{
    public class Save
    {
        private static bool NeedsHtmlTagEncode(SaveArgs.SaveField field)
        {
            return field.ID == FieldIDs.DisplayName;
        }

        public void Process(SaveArgs args)
        {
            SaveArgs.SaveItem[] items = args.Items;
            for (int i = 0; i < items.Length; i++)
            {
                SaveArgs.SaveItem saveItem = items[i];
                Item item = Context.ContentDatabase.Items.GetItem(saveItem.ID, saveItem.Language, saveItem.Version);
                if (item != null)
                {
                    if (item.Locking.IsLocked() && !item.Locking.HasLock() && !Context.User.IsAdministrator && !args.PolicyBasedLocking)
                    {
                        args.Error = "Could not modify locked item " + item.Name;
                        args.AbortPipeline();
                        return;
                    }
                    item.Editing.BeginEdit();
                    SaveArgs.SaveField[] fields = saveItem.Fields;
                    for (int j = 0; j < fields.Length; j++)
                    {
                        SaveArgs.SaveField saveField = fields[j];
                        Field field = item.Fields[saveField.ID];

                        if (field != null && (!field.IsBlobField || (field.TypeKey != "attachment" && saveField.Value != Translate.Text("[Blob Value]"))))
                        {
                            saveField.OriginalValue = field.Value;
                            if (saveField.OriginalValue != saveField.Value)
                            {
                                if (!string.IsNullOrEmpty(saveField.Value))
                                {
                                    if (field.TypeKey == "rich text" && Settings.HtmlEditor.RemoveScripts)
                                    {
                                        saveField.Value = WebUtil.RemoveAllScripts(saveField.Value);
                                    }
                                    if (Save.NeedsHtmlTagEncode(saveField))
                                    {
                                        saveField.Value = saveField.Value.Replace("<", "&lt;").Replace(">", "&gt;");
                                    }
                                    if (field.TypeKey == "general link" || field.TypeKey == "general link with search")
                                    {
                                        saveField.Value = Uri.UnescapeDataString(saveField.Value).Replace("&", "&amp;");
                                    }
                                }
                                field.Value = saveField.Value;
                            }
                        }
                    }
                    item.Editing.EndEdit();
                    Log.Audit(this, "Save item: {0}", new string[]
					{
						AuditFormatter.FormatItem(item)
					});
                    args.SavedItems.Add(item);
                }
            }
            if (!Context.IsUnitTesting)
            {
                Context.ClientPage.Modified = false;
            }
            if (args.SaveAnimation)
            {
                Context.ClientPage.ClientResponse.Eval("var d = new scSaveAnimation('ContentEditor')");
                return;
            }
        }
    }
}