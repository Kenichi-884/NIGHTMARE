using UnityEngine;

namespace SickscoreGames.ExampleScene
{
	public class ExampleBouncePrism : MonoBehaviour
	{
		#region Variables
		[Range(0f, 100f)]
		public float bounceSpeed = 3f;
		public float bounceHeight = .15f;
		private Vector3 _pos;
		#endregion


		#region Main Methods
		void Start ()
		{
			_pos = transform.position;
		}


		void Update ()
		{
			// bounce prism up & down
			if (bounceSpeed > 0f)
			{
				float newY = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight + _pos.y;
				transform.position = new Vector3(transform.position.x, newY, transform.position.z);
			}
		}
		#endregion
	}
}
