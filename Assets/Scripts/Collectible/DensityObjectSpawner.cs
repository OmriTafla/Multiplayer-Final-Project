using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Collectible
{
    public class DensityObjectSpawner : NetworkBehaviour
    {
        [SerializeField] private Transform[] areaCorners;
        [SerializeField] private float targetDensity, spawnInterval; 
        [SerializeField, Tooltip("")] private float kernelSize;
        [SerializeField] private NetworkObject prefabToSpawn;
        
        private const int MAX_NUM_FAILS = 100;
        private const int MAX_TRACKED_OBJECTS = 1000;
        // [Networked, Capacity(MAX_TRACKED_OBJECTS)]
        private readonly List<NetworkObject> spawnedObjects = new();
        
        private bool spawnCoroutineDone = false;

        private void OnValidate()
        {
            if (areaCorners == null || areaCorners.Length < 2)
            {
                return;
            }

            if (areaCorners.Length <= 2) return;
            
            areaCorners = new Span<Transform>(areaCorners,0,2).ToArray();
            Debug.LogWarning("Spawner area must only have 2 corners");
        }

        public override void Spawned()
        {
            base.Spawned();

            spawnCoroutineDone = false;
            StartCoroutine(SpawnSequence());
        }

        private IEnumerator SpawnObjectsCoroutine(int maxNumFails, float interval, bool tryOnce = false)
        {
            Func<int, bool> outOfAttempts = maxNumFails == -1 ? 
                _ => false :
                numFails => numFails >= maxNumFails;
            
            float kernelVolume = 4f / 3 * math.PI * math.pow(kernelSize, 3);
            float radiusSquared = math.pow(kernelSize, 2);
                

            while (true)
            {
                int numFails = 0;
                while (!outOfAttempts(numFails))
                {
                    Vector3 spawnPosition = new Vector3(
                        Random.Range(areaCorners[0].position.x, areaCorners[1].position.x),
                        Random.Range(areaCorners[0].position.y, areaCorners[1].position.y),
                        Random.Range(areaCorners[0].position.z, areaCorners[1].position.z));

                    spawnedObjects.RemoveAll(obj => !obj.IsValid);
                    
                    int numItemsInKernel = spawnedObjects
                        .Count(o => (o.transform.position - spawnPosition).sqrMagnitude <= radiusSquared);

                    if (numItemsInKernel / kernelVolume >= targetDensity)
                    {
                        numFails++;
                    }
                    else
                    {
                        spawnedObjects.Add(SinglePeer_NetworkRunnerManager.Instance.NetworkRunner.Spawn(prefabToSpawn, spawnPosition));
                    }
                }

                if (tryOnce) break;
                
                yield return new WaitForSeconds(interval);
            }
            
            spawnCoroutineDone = true;
        }

        private IEnumerator SpawnSequence()
        {
            yield return SpawnObjectsCoroutine(maxNumFails: MAX_NUM_FAILS, 0f, tryOnce: true);
            
            yield return new WaitUntil(() => spawnCoroutineDone);
            
            yield return SpawnObjectsCoroutine(maxNumFails: MAX_NUM_FAILS, interval: spawnInterval);
        }
    }
}