using UnityEngine;
using Valve.VR.InteractionSystem;
using Zombies;

namespace Game
{
    public class ZombieGameManager : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Playing,
            Cleared
        }

        [SerializeField] private HoverButton _startButton;
        [SerializeField] private GameHud _hud;
        [SerializeField] private Zombie[] _zombies;

        private State _state = State.Idle;
        private int _aliveCount;

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onButtonDown.AddListener(_ => OnStartButtonPressed());
        }

        private void Start()
        {
            foreach (var zombie in _zombies)
                zombie.OnDied += OnZombieDied;

            ResetLevel();
            _hud?.SetMessage("PRESS START TO RELEASE THE ZOMBIES");
        }

        private void OnStartButtonPressed()
        {
            StartGame();
        }

        private void StartGame()
        {
            ResetLevel();
            _state = State.Playing;
            _aliveCount = _zombies.Length;
            _hud?.SetMessage(string.Empty);

            foreach (var zombie in _zombies)
                zombie.Activate();
        }

        private void ResetLevel()
        {
            foreach (var zombie in _zombies)
                zombie.ResetZombie();
        }

        private void OnZombieDied()
        {
            if (_state != State.Playing) return;

            _aliveCount--;
            if (_aliveCount > 0) return;

            _state = State.Cleared;
            _hud?.SetMessage("ALL CLEAR\nPRESS START TO GO AGAIN");
        }
    }
}
