## Reinforcement Learning based Movement in Unity

This project aims to replicate and explore the use of Reinforcement Learning in unity from guiding to fine controls. 

Leveraging the already built `mlagents` plugin for Unity and will likely expand to other learning algorithms once I finally get tired of PPO.


# Development

The code envrionment for this is Unity + UV.

[Install UV Here](https://docs.astral.sh/uv/getting-started/installation/)


## Training
In order to train the agent must be set to the default and then running the commands. Since training is not yet running headless you will have to be focused on Unity for it to trian. 

- Make sure if you are training to duplicate the scene up to 6 times and to have multiple environments running at once.

```bash
uv sync
source .venv/Scripts/activate
mlagents-learn config/flying_agent.yaml --run-id=4-points-2 --force
```


[Docs for this package](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Learning-Environment-Design-Agents.html)


## Seeing the agent run

Copy the `.onnx` model from training into the `Assets/models` folder, and then selecting that `.onnx` file in the Agent. Flip it from `default mode` to `inference only`. If you wish to test out flying/doing things by hand run the `heuristic mode` which is bound to Arrow Keys/WASD

