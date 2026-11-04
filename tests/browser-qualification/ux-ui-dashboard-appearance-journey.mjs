import {runJourney as dashboard} from './ux-ui-dashboard-journey.mjs';
export async function runJourney(options){return dashboard({...options,appearanceOnly:true});}
