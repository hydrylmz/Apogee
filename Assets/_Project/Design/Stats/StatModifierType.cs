namespace Apogee.Design
{
    /// <summary>
    /// Evaluation pipeline stages for statistical modifiers.
    /// Evaluation sequence: Flat -> PercentAdd -> PercentMult.
    /// </summary>
    public enum StatModifierType
    {
        /// <summary>
        /// Added directly to base value: (Base + Sum(Flat)).
        /// E.g. +1.5 metric tons structure mass.
        /// </summary>
        Flat = 100,

        /// <summary>
        /// Summed percentage applied to (Base + Flat): (Base + Flat) * (1 + Sum(PercentAdd)).
        /// E.g. +0.10 (+10%) from supplier bonus and +0.05 (+5%) from research = +15% total.
        /// </summary>
        PercentAdd = 200,

        /// <summary>
        /// Multiplicative compounding percentage applied sequentially: Value * (1 + PercentMult).
        /// E.g. -0.20 (-20%) harsh radiation degradation.
        /// </summary>
        PercentMult = 300
    }
}
