using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class EnvironmentManager : MonoBehaviour
{
    [Serializable]
    public class AreaInfo
    {
        // Remove explicit areaName field and use a property to infer from GameObject
        private string _areaName => areaObject != null ? areaObject.name : "";
        public string areaName => _areaName;
        public GameObject areaObject;
        [HideInInspector]
        public List<LocationInfo> locations = new List<LocationInfo>();
    }

    [Serializable]
    public class LocationInfo
    {
        // Make the locationName property private (but with a public getter for access)
        private string _locationName => locationObject != null ? locationObject.name : "";
        public string locationName => _locationName;
        public GameObject locationObject;
        public string parentAreaName;
    }

    // Simplify to just use GameObjects as input
    [SerializeField] private List<GameObject> areaObjects = new List<GameObject>();
    
    // Internal list of processed areas
    private List<AreaInfo> areas = new List<AreaInfo>();
    
    // Cached list of all locations across all areas
    private List<LocationInfo> allLocations = new List<LocationInfo>();

    private void Awake()
    {
        InitializeAreas();
        PopulateLocations();
    }

    private void InitializeAreas()
    {
        areas.Clear();
        
        // Convert GameObject references to AreaInfo objects
        foreach (var areaObject in areaObjects)
        {
            if (areaObject == null) continue;
            
            AreaInfo area = new AreaInfo
            {
                areaObject = areaObject
            };
            
            areas.Add(area);
        }
    }

    private void PopulateLocations()
    {
        allLocations.Clear();
        
        foreach (var area in areas)
        {
            area.locations.Clear();
            
            if (area.areaObject == null) continue;
            
            // Get all child objects as locations
            foreach (Transform child in area.areaObject.transform)
            {
                LocationInfo location = new LocationInfo
                {
                    locationObject = child.gameObject,
                    parentAreaName = area.areaName
                };
                
                area.locations.Add(location);
                allLocations.Add(location);
            }
        }
        
        Debug.Log($"Environment initialized with {areas.Count} areas and {allLocations.Count} locations");
    }

    public List<AreaInfo> GetAllAreas()
    {
        return areas;
    }

    public List<LocationInfo> GetAllLocations()
    {
        return allLocations;
    }

    public List<LocationInfo> GetLocationsInArea(string areaName)
    {
        var area = areas.Find(a => a.areaName == areaName);
        return area != null ? area.locations : new List<LocationInfo>();
    }

    public LocationInfo GetLocationByName(string locationName)
    {
        return allLocations.Find(loc => loc.locationName == locationName);
    }

    public GameObject GetLocationObject(string locationName)
    {
        var location = GetLocationByName(locationName);
        return location?.locationObject;
    }

    // Find closest location to a given position
    public LocationInfo GetClosestLocation(Vector3 position)
    {
        if (allLocations.Count == 0) return null;
        
        return allLocations
            .OrderBy(loc => Vector3.Distance(loc.locationObject.transform.position, position))
            .FirstOrDefault();
    }

    // Find a random location in a specific area
    public LocationInfo GetRandomLocationInArea(string areaName)
    {
        var locations = GetLocationsInArea(areaName);
        if (locations.Count == 0) return null;
        
        int randomIndex = UnityEngine.Random.Range(0, locations.Count);
        return locations[randomIndex];
    }

    // Find a random location in any area
    public LocationInfo GetRandomLocation()
    {
        if (allLocations.Count == 0) return null;
        
        int randomIndex = UnityEngine.Random.Range(0, allLocations.Count);
        return allLocations[randomIndex];
    }
} 