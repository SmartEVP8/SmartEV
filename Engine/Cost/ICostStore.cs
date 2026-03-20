namespace Engine.Cost;

public interface ICostStore
{
    void TrySet(CostWeights update, long seq);
    CostWeights GetWeights();
}

