using System.Collections.Generic;
using System.Linq;
using Enums;
using Fusion;
using UnityEngine;

public class SessionListUI : MonoBehaviour
{
    [SerializeField] private SessionItemUI sessionItemPrefab;
    [SerializeField] private Transform listContainer;
    private List<SessionInfo> sessionList;

    private IOGameMode FilterByGameMode
    {
        get => filterByGameMode;
        set
        {
            filterByGameMode = value;
            ShowSessionList();
        }
    }

    private IOGameMode filterByGameMode = IOGameMode.Any;
    
    public void ShowSessionList(List<SessionInfo> sessionList)
    {
        this.sessionList = sessionList;
        ShowSessionList();
    }

    private void ShowSessionList()
    {
        ClearList();

        //filter by game mode
        var filteredSessionList = sessionList.Where(SessionGameModeFitsFilter);
        
        foreach (SessionInfo sessionInfo in filteredSessionList)
        {
            if (!sessionInfo.IsVisible)
            {
                Debug.LogWarning("Received hidden session! This shouldn't happen");
                continue;
            }
            
            var newItem = Instantiate(sessionItemPrefab, listContainer);
            
            newItem.SetSessionInfo(sessionInfo);
            
            newItem.SetCallback(() => SessionJoiner.Instance.JoinSpecificSession(sessionInfo));
        }
    }

    public void SetFilterByGameMode(int gameMode) => FilterByGameMode = (IOGameMode)gameMode;

    private void ClearList()
    {
        List<GameObject> itemsToDelete = new();
        foreach (Transform child in listContainer)
        {
            itemsToDelete.Add(child.gameObject);
        }
        itemsToDelete.ForEach(Destroy);
    }

    private bool SessionGameModeFitsFilter(SessionInfo session)
    {
        if (FilterByGameMode == IOGameMode.Any) 
            return true;

        if (!session.Properties.ContainsKey("GameMode"))
        {
            Debug.LogWarning($"Session {session.Name} does not have a GameMode property");
            return true;
        }
        
        IOGameMode gameMode = (IOGameMode)session.Properties[SessionJoiner.GAMEMODE_PROPERTY_NAME].PropertyValue;
        return gameMode == FilterByGameMode;
    }
}
