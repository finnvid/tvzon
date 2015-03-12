using System;
using System.Collections;
using System.Collections.Specialized;
using System.Xml.Serialization;

[Serializable]
public class SerializableStringDictionary : StringDictionary,
IXmlSerializable {

	#region Node class
	[Serializable]
	public class Node {
		public Node() { }

		public Node(string k, string v) {
			key = k;
			val = v;
		}

		public string key;
		public string val;
	}

	#endregion Node class for XML Serialization

	#region IXmlSerializable Members

	public System.Xml.Schema.XmlSchema GetSchema() {
		return null;
	}

	public void ReadXml(System.Xml.XmlReader reader) {
		XmlSerializer x = new
		XmlSerializer(typeof(System.Collections.ArrayList), new System.Type[] {
typeof(Node) });

		reader.Read();
		ArrayList list = x.Deserialize(reader) as ArrayList;

		if (list == null)
			return;

		foreach (Node node in list) {
			Add(node.key, node.val);
		}
	}

	public void WriteXml(System.Xml.XmlWriter writer) {
		XmlSerializer x = new
		XmlSerializer(typeof(System.Collections.ArrayList), new System.Type[] {
typeof(Node) });
		ArrayList list = new ArrayList();
		foreach (string key in this.Keys) {
			list.Add(new Node(key, this[key]));
		}
		x.Serialize(writer, list);
	}

	#endregion
}