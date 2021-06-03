using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DynamicMissionGeneratorAssembly
{
	public class MissionListItem : MonoBehaviour
	{
		public event EventHandler Click;

		public Text NameText;
		//public GameObject FolderIcon;

		private new string name;
		public string Name
		{
			get => this.name;
			set { this.name = value; this.NameText.text = value; }
		}

		private bool folder;
		public bool Folder
		{
			get => this.folder;
			set { this.folder = value; this.NameText.color = value ? Color.red : Color.black; }
		}

		public void OnClick() { this.Click?.Invoke(this, EventArgs.Empty); }

		public void HighlightName(int startIndex, int length)
			=> this.NameText.text = this.name.Insert(startIndex + length, "</color>").Insert(startIndex, "<color=red>");
	}
}
