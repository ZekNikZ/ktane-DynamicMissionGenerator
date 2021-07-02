using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DynamicMissionGeneratorAssembly
{
	public class MissionsPage : MonoBehaviour
	{
		public KMSelectable SwitchSelectable;
		public KMSelectable FolderSelectable;
		public KMSelectable BackFolderSelectable;
		public KMSelectable AddFolderSelectable;

		public MissionListItem MissionListItemPrefab;
		public RectTransform MissionList;
		public GameObject NoMissionsText;
		public Text FolderText;
		public RectTransform ContextMenu;
		public RectTransform CanvasTransform;
		public Prompt Prompt;
		public Alert Alert;

		public KMAudio Audio;

		private string subfolder = null;
		private string missionsFolder => subfolder == null ? DynamicMissionGenerator.MissionsFolder : Path.Combine(DynamicMissionGenerator.MissionsFolder, subfolder);
		private Dictionary<string, MissionEntry> entries = new Dictionary<string, MissionEntry>();
		private MissionEntry contextMenuMission;

		public string Subfolder
		{
			get { return this.subfolder; }
			set { this.subfolder = value; LoadMissions(); DynamicMissionGenerator.CurrentMissionFolder = value; }
		}

		private void BackFolder()
		{
			if (this.Subfolder == null) return;

			var newFolder = Path.GetDirectoryName(this.Subfolder);
			this.Subfolder = newFolder == "" ? null : newFolder;
		}

		public void Start()
		{
			if (Application.isEditor) return;
			DynamicMissionGenerator.Instance.MissionsPage = this;

			LoadMissions();
			WatchMissions();

			Action goBack = (Action)DynamicMissionGenerator.ModSelectorApi["GoBackMethod"];
			SwitchSelectable.OnInteract += () => { goBack(); return false; };

			FolderSelectable.OnInteract += () => { Application.OpenURL($"file://{missionsFolder}"); return false; };

			BackFolderSelectable.OnInteract += () => { BackFolder(); return false; };

			AddFolderSelectable.OnInteract += () => {
				Prompt.MakePrompt("Add Folder", "", CanvasTransform, SwitchSelectable.Parent, Audio, name => {
					var targetPath = Path.Combine(missionsFolder, name);
					if (Directory.Exists(targetPath))
					{
						Alert.MakeAlert("Directory Exists", "A directory with that name already exists.", CanvasTransform, SwitchSelectable.Parent);
						return;
					}

					Directory.CreateDirectory(targetPath);
					LoadMissions();
				});
				return false; 
			};

			foreach (Transform button in ContextMenu.transform)
			{
				button.GetComponent<Button>().onClick.AddListener(() => MenuClick(button));
			}
		}

		private void LoadMissions()
		{
			BackFolderSelectable.gameObject.SetActive(this.Subfolder != null);
			FolderText.text = (this.Subfolder == null ? Path.GetFileName(DynamicMissionGenerator.MissionsFolder) : Path.Combine(Path.GetFileName(DynamicMissionGenerator.MissionsFolder), this.Subfolder)).Replace("\\", "/") + "/";

			foreach (MissionEntry entry in entries.Values)
				Destroy(entry.Item.gameObject);
			entries.Clear();

			// Folders
			var folders = Directory.GetDirectories(missionsFolder).ToDictionary(Path.GetFileNameWithoutExtension, file => {
				var name = Path.GetFileNameWithoutExtension(file);
				MissionEntry mission = null;

				var item = Instantiate(MissionListItemPrefab);
				item.Name = name;
				item.Folder = true;
				item.transform.SetParent(MissionList, false);
				item.GetComponent<Button>().onClick.AddListener(() => {
					if (Input.GetKey(KeyCode.LeftShift))
						return;

					this.Subfolder = this.Subfolder == null ? name : Path.Combine(this.Subfolder, name);
				});

				mission = new MissionEntry(name, file, item, false);
				return mission;
			});


			// Missions
			var missions = Directory.GetFiles(missionsFolder).ToDictionary(Path.GetFileNameWithoutExtension, file => {
				var name = Path.GetFileNameWithoutExtension(file);
				MissionEntry mission = null;

				var item = Instantiate(MissionListItemPrefab);
				item.Name = name;
				item.Folder = false;
				item.transform.SetParent(MissionList, false);
				item.GetComponent<Button>().onClick.AddListener(() => {
					if (Input.GetKey(KeyCode.LeftShift))
						return;

					SwitchSelectable.OnInteract();
					DynamicMissionGenerator.Instance.InputPage.LoadMission(mission);
				});

				mission = new MissionEntry(name, File.ReadAllText(file), item, false);
				return mission;
			});

			entries = folders;
			foreach (var entry in missions)
				entries.Add(entry.Key, entry.Value);

			NoMissionsText.SetActive(entries.Count == 0);
		}

		private void WatchMissions()
		{
			void updateMissions(object _, FileSystemEventArgs __) 
			{
				LoadMissions();
			}

			var watcher = new FileSystemWatcher
			{
				Path = missionsFolder,
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
			};
			watcher.Created += updateMissions;
			watcher.Changed += updateMissions;
			watcher.Deleted += updateMissions;
			watcher.EnableRaisingEvents = true;
		}

		private void Update()
		{
			if (Input.GetMouseButtonUp(0))
			{
				PointerEventData pointerData = new PointerEventData(EventSystem.current) {
					position = Input.mousePosition
				};
				
				List<RaycastResult> results = new List<RaycastResult>();
				EventSystem.current.RaycastAll(pointerData, results);
				
				if (!Input.GetKey(KeyCode.LeftShift) || results.Count == 0 || results[0].gameObject.transform.parent.GetComponent<MissionListItem>() == null)
				{
					ContextMenu.gameObject.SetActive(false);
					return;
				}

				RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasTransform, Input.mousePosition, Camera.main, out Vector2 localPoint);
				ContextMenu.localPosition = localPoint;
				ContextMenu.gameObject.SetActive(true);

				var result = results[0];
				contextMenuMission = entries.First(pair => pair.Value.Item == result.gameObject.transform.parent.GetComponent<MissionListItem>()).Value;
			}
		}

		private void MenuClick(Transform button)
		{
			switch (button.name)
			{
				case "Rename":
					Prompt.MakePrompt("Rename Mission", contextMenuMission.Name, CanvasTransform, SwitchSelectable.Parent, Audio, name => {
						var targetPath = Path.Combine(missionsFolder, name + ".txt");
						if (File.Exists(targetPath))
						{
							Alert.MakeAlert("Mission Exists", "A mission with that name already exists.", CanvasTransform, SwitchSelectable.Parent);
							return;
						}

						File.Move(Path.Combine(missionsFolder, contextMenuMission.Name + ".txt"), targetPath);
						LoadMissions();
					});
					break;
				case "Duplicate":
					Prompt.MakePrompt("New Mission Name", contextMenuMission.Name + " (Copy)", CanvasTransform, SwitchSelectable.Parent, Audio, name => {
						var targetPath = Path.Combine(missionsFolder, name + ".txt");
						if (File.Exists(targetPath))
						{
							Alert.MakeAlert("Mission Exists", "A mission with that name already exists.", CanvasTransform, SwitchSelectable.Parent);
							return;
						}

						File.Copy(Path.Combine(missionsFolder, contextMenuMission.Name + ".txt"), targetPath);
						LoadMissions();
					});
					break;
				case "Delete":
					File.Delete(Path.Combine(missionsFolder, contextMenuMission.Name + ".txt"));
					LoadMissions();
					break;
			}
		}
		
		public class MissionEntry
		{
			public string Name;
			public string Content;
			public MissionListItem Item;
			public bool Folder;

			public MissionEntry(string name, string content, MissionListItem item, bool folder)
			{
				Name = name;
				Content = content;
				Item = item;
				Folder = folder;
			}
		}
	}
}
