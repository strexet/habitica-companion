import {runJourney as gameJourney} from './ux-ui-game-names-journey.mjs';
export const runJourney=args=>gameJourney({...args,inventoryOnly:true});
