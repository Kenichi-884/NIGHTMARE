/*
 * StylizedWeatherParticleWrapper.cs
 * ---------------------------------
 * Part of the Unluck Software Stylized Weather System.
 *
 * PURPOSE:
 *  - Keeps weather particles looping seamlessly by "wrapping" them
 *    when they move outside the system's bounds.
 *  - Ensures continuous rain/snow/fog effects around a moving camera or player.
 *
 * HOW IT WORKS:
 *  - Periodically checks active particles (based on updateDelay).
 *  - When a particle exits the defined X/Z limits, it is teleported
 *    to the opposite side of the system’s area.
 *  - Optionally adjusts lifetime or velocity for smoother re-entry.
 *  - The entire particle system follows a designated follow object (e.g., camera).
 *
 * KEY SETTINGS:
 *  - wrapParticles: Toggles wrapping on/off.
 *  - updateDelay: How often to process wrapping (seconds).
 *  - changeLifeTimeAfterWrap: Reduces lifetime after wrap by lifetimeAfterWrap.
 *  - changeVelocityAfterWrap: Reduces velocity after wrap by velocityMultiplierAfterWrap.
 *
 * INTERNALS:
 *  - Uses ParticleSystem.GetParticles() / SetParticles() for direct manipulation.
 *  - Calculates bounds from the system’s shape scale.
 *  - Controlled by a parent StylizedWeatherController.
 *  - Includes gizmo visualization of wrap limits in the Editor.
 *
 * USAGE:
 *  - Attach to a ParticleSystem GameObject under a StylizedWeatherController.
 *  - Commonly used for looping rain, snow, fog, or dust effects.
 *
 * AUTHOR: Unluck Software
 */

namespace UnluckSoftware
{
	using UnityEngine;
#if UNITY_EDITOR
	using UnityEditor;
#endif

	[System.Serializable]
	public class StylizedWeatherParticleWrapper :MonoBehaviour
	{
		public bool wrapParticles = true;
		[Range(0.01f, 0.5f)] public float updateDelay = 0.2f;

		[Header("")]
		[SerializeField] private bool changeLifeTimeAfterWrap;
		[SerializeField][Range(0.0f, 1f)] private float lifetimeAfterWrap = 0.5f;
		[SerializeField] private bool changeVelocityAfterWrap;
		[SerializeField][Range(0.0f, 1f)] private float velocityMultiplierAfterWrap = 0.5f;

		[HideInInspector][SerializeField] private ParticleSystem m_system;
		private ParticleSystem.Particle[] m_particles;

		private Transform cancheTransform;
		private Transform followObject;
		private float followOffset;
		private float xLimit = 5f;
		private float zLimit = 5f;

		private StylizedWeatherController controller;

		private void Start()
		{
			GetReferences();
			m_particles = new ParticleSystem.Particle[m_system.main.maxParticles];

			if (followObject != null)
			{
				followOffset = m_system.transform.position.y -followObject.transform.position.y;
			}

			ScaleToParticleSystem();
		}

		public void GetReferences()
		{
			if (!cancheTransform) cancheTransform = transform;
			if (!m_system) m_system = GetComponent<ParticleSystem>();

			if (!controller)
			{
				if (transform.parent && transform.parent.parent)
				{
					controller = transform.parent.parent.GetComponent<StylizedWeatherController>();
				}
			}

			if (!followObject)
			{
				if (controller)
				{
					followObject = controller.followObject;
				} 
			}
		}

		private void ScaleToParticleSystem()
		{
			xLimit = m_system.shape.scale.x * .5f;
			zLimit = m_system.shape.scale.y * .5f;
		}

		private void OnEnable()
		{
			Invoke("WrapParticles", Random.Range(1f, 0.5f));
		}

		private void OnDisable()
		{
			CancelInvoke();
		}

		void Update()
		{
			if (followObject == null) return;

			Vector3 position = followObject.position;
			position.y += followOffset;
			cancheTransform.position = position;
		}

		void WrapParticles()
		{
			Invoke("WrapParticles", updateDelay);
			if (!wrapParticles) return;

			int numParticlesAlive = m_system.GetParticles(m_particles);
			if (numParticlesAlive == 0) return;

			Vector3 pos;
			Vector3 transformOffset = transform.position;
			float outside;
			float dif;
			Vector3 vel;

			for (int i = 0; i < numParticlesAlive; i++)
			{
				pos = m_particles[i].position;
				outside = xLimit + transformOffset.x;
				vel = m_particles[i].velocity;

				if (m_particles[i].position.x > outside)
				{
					dif = m_particles[i].position.x - outside;
					pos.x = -xLimit + transformOffset.x + dif;
					m_particles[i].position = pos;

					if (changeLifeTimeAfterWrap && m_system.emission.rateOverTimeMultiplier > 0f)
						m_particles[i].remainingLifetime = lifetimeAfterWrap * m_particles[i].startLifetime;

					if (changeVelocityAfterWrap)
					{
						vel.x *= velocityMultiplierAfterWrap;
						m_particles[i].velocity = vel;
					}

					continue;
				}

				outside = -xLimit + transformOffset.x;
				if (m_particles[i].position.x < outside)
				{
					dif = m_particles[i].position.x - outside;
					pos.x = xLimit + transformOffset.x + dif;
					m_particles[i].position = pos;

					if (changeLifeTimeAfterWrap && m_system.emission.rateOverTimeMultiplier > 0f)
						m_particles[i].remainingLifetime = lifetimeAfterWrap * m_particles[i].startLifetime;

					if (changeVelocityAfterWrap)
					{
						vel.x *= velocityMultiplierAfterWrap;
						m_particles[i].velocity = vel;
					}

					continue;
				}

				outside = zLimit + transformOffset.z;
				if (m_particles[i].position.z > outside)
				{
					dif = m_particles[i].position.z - outside;
					pos.z = -zLimit + transformOffset.z + dif;
					m_particles[i].position = pos;

					if (changeLifeTimeAfterWrap && m_system.emission.rateOverTimeMultiplier > 0f)
						m_particles[i].remainingLifetime = lifetimeAfterWrap * m_particles[i].startLifetime;

					if (changeVelocityAfterWrap)
					{
						vel.x *= velocityMultiplierAfterWrap;
						m_particles[i].velocity = vel;
					}

					continue;
				}

				outside = -zLimit + transformOffset.z;
				if (m_particles[i].position.z < outside)
				{
					dif = m_particles[i].position.z - outside;
					pos.z = zLimit + transformOffset.z + dif;
					m_particles[i].position = pos;

					if (changeLifeTimeAfterWrap && m_system.emission.rateOverTimeMultiplier > 0f)
						m_particles[i].remainingLifetime = lifetimeAfterWrap * m_particles[i].startLifetime;

					if (changeVelocityAfterWrap)
					{
						vel.x *= velocityMultiplierAfterWrap;
						m_particles[i].velocity = vel;
					}

					continue;
				}
			}

			m_system.SetParticles(m_particles, numParticlesAlive);
		}

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			GetReferences();

			if (controller && !controller.drawGizmos) return;
			if (!controller && !wrapParticles) return;

			Gizmos.color = Color.red;

			Gizmos.DrawCube(transform.position + new Vector3(xLimit, 0, 0), new Vector3(.1f, .1f, zLimit * 2));
			Gizmos.DrawCube(transform.position + new Vector3(-xLimit, 0, 0), new Vector3(.1f, .1f, zLimit * 2));
			Gizmos.DrawCube(transform.position + new Vector3(0, 0, zLimit), new Vector3(xLimit * 2, .1f, .1f));
			Gizmos.DrawCube(transform.position + new Vector3(0, 0, -zLimit), new Vector3(xLimit * 2, .1f, .1f));

			if (!m_system) m_system = GetComponent<ParticleSystem>();
			ScaleToParticleSystem();
		}
#endif
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(StylizedWeatherParticleWrapper))]
	public class StylizedWeatherParticleWrapperEditor :Editor
	{
		public override void OnInspectorGUI()
		{
			var tar = target as StylizedWeatherParticleWrapper;

			if (!tar.wrapParticles)
				EditorGUILayout.HelpBox("Wrapping is disable, particle system will just follow.", MessageType.Info);

			DrawDefaultInspector();
			tar.GetReferences();
		} 
	}
#endif
}
