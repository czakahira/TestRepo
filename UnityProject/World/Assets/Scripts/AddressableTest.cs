using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using TMPro;

public class AddressableTest : MonoBehaviour
{
	public AddressableManager addressable;

	[SerializeField] private string m_AssetsPath;
	[SerializeField] private AssetReference m_AssetReference;
	[SerializeField] private int m_InstantiateCount;

	private AddressableLoader m_Loader = null;

	private StringBuilder builder = new StringBuilder();
	[SerializeField] private TextMeshProUGUI text;
	private float start;

	// Start is called before the first frame update
	void Start()
	{	
		Debug.Log(Addressables.LibraryPath);
	}

	private void OnLoaded(object result)
	{
		GameObject obj = Instantiate((GameObject)result);
		obj.name = $"Cube";
		obj.transform.SetParent(this.transform);
	}
	private void OnLoadComp(){
		float end = Time.time;
		AddText($"LoadCom : {end} ({end - start})\n");
	}
	private void AddText(string add){
		builder.Append(add);
		text.text = builder.ToString();
	}

	// Update is called once per frame
	void Update()
    {
		if (Keyboard.current.spaceKey.wasPressedThisFrame) {
			if(m_Loader == null){ 
				m_Loader = addressable.GetLoader();
				start = Time.time;
				AddText($"Befor : {start}\n");
				for (int i = 0; i < m_InstantiateCount; i++) {
					m_Loader.Request(m_AssetsPath, OnLoaded);
				}
				m_Loader.Start(OnLoadComp);
				AddText($"After : {Time.time}\n");
			}
		}

	}
}
