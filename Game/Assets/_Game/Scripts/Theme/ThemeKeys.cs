// Every key a ThemeProfile can name. Use these constants in code instead of raw strings,
// so a typo is a compile error rather than a missing name at runtime.
public static class ThemeKeys
{
    // Identity (stored as dedicated fields on ThemeProfile)
    public const string InnName = "inn.name";
    public const string CurrencyName = "currency.name";
    public const string CurrencySymbol = "currency.symbol";

    // Staff roles
    public const string RoleClerk = "role.clerk";
    public const string RolePorter = "role.porter";
    public const string RoleServer = "role.server";
    public const string RoleChambermaid = "role.chambermaid";
    public const string RoleCook = "role.cook";

    // Guest types
    public const string GuestPilgrim = "guest.pilgrim";
    public const string GuestTraveller = "guest.traveller";
    public const string GuestMerchant = "guest.merchant";
    public const string GuestNoble = "guest.noble";

    // Room types
    public const string RoomBunk = "room.bunk";
    public const string RoomCommon = "room.common";
    public const string RoomPrivate = "room.private";
    public const string RoomSuite = "room.suite";

    // Services
    public const string ServiceMeal = "service.meal";
    public const string ServiceDrink = "service.drink";
    public const string ServiceFirewood = "service.firewood";
    public const string ServiceBath = "service.bath";
    public const string ServiceTurndown = "service.turndown";
    public const string ServiceBootPolish = "service.boot_polish";
    public const string ServiceWakeUp = "service.wake_up";
    public const string ServiceLuggage = "service.luggage";

    // Staff tiers
    public const string TierD = "tier.d";
    public const string TierC = "tier.c";
    public const string TierB = "tier.b";
    public const string TierA = "tier.a";
    public const string TierS = "tier.s";

    // Places
    public const string PlaceFrontDesk = "place.front_desk";
    public const string PlaceKitchen = "place.kitchen";
    public const string PlaceLaundry = "place.laundry";
    public const string PlaceRestArea = "place.rest_area";
    public const string PlaceYard = "place.yard";

    // Props (graybox blocks a theme can replace with a prefab)
    public const string PropBed = "prop.bed";
    public const string PropBedDouble = "prop.bed_double";
    public const string PropRoomTable = "prop.room_table";
    public const string PropFrontDesk = "prop.front_desk";
    public const string PropTaproomTable = "prop.taproom_table";
    public const string PropStove = "prop.stove";
    public const string PropCopper = "prop.copper";
    public const string PropTub = "prop.tub";
    public const string PropLaundryTub = "prop.laundry_tub";
    public const string PropWoodpile = "prop.woodpile";
    public const string PropSeat = "prop.seat";
}
