import {CharacterType, MapType} from "./Evos";

export enum BannerType {
    background = "Background",
    foreground = "Foreground"
}

export function logo() {
    return `/img/logo.png`;
}
export function logoSmall() {
    return `/logo.png`;
}

export function playerBanner(type: BannerType, id: number) {
    return `/img/banners/${type}/${id}.png`;
}

export function characterIcon(characterType: CharacterType) {
    if (characterType === CharacterType.None || characterType === CharacterType.FemaleWillFill || characterType === CharacterType.PendingWillFill) {
        return `/img/characters/icons/Default.png`;
    }
    return `/img/characters/icons/${characterType}.png`;
}

export function mapMiniPic(map: MapType) {
    return `/img/maps/mini/${map}.png`;
}


