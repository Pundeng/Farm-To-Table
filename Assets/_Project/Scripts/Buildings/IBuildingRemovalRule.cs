namespace CozyFoodFactory.Buildings
{
    public interface IBuildingRemovalRule
    {
        bool CanRemove { get; }
    }

    public interface IBuildingMoveState
    {
        bool CanMove { get; }

        void DetachForMove();
    }
}
